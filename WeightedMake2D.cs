/*
------------------------------

This command creates a weighted 2D representation of user-selected 3D geometry.
It weighs the 2D lines based on formal relationships between the source geometry
edges and its adjacent faces. If an edge only has one adjacent face, or one of
its two adjacent faces is hidden, it is defined as an "WT_Outline". If both faces are
present and the line is on a convex corner, it is defined as "WT_Convex". All other
visible lines are defined as "WT_Concave". Hidden lines are also processed. Results
are baked onto layers according to their assigned weight.

------------------------------
created 11/28/2023
Ennead Architects

Chloe Xu
chloe.xu@ennead.com
edited:11/30/2023

------------------------------
*/

using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AutoLineWeight
{
    public class WeightedMake2D : Command
    {
        /// <summary>
        /// creates a weighted 2D representation of user-selected 3D geometry based
        /// on formal relationships between user-selected source geometry and its
        /// adjacent faces.
        /// </summary>
        public WeightedMake2D()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static WeightedMake2D Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "WeightedMake2D";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Aquires current viewport
            RhinoViewport currentViewport;
            currentViewport = RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport;

            UserSelectGeometry selectObjs = new UserSelectGeometry();
            ObjRef[] objRefs = selectObjs.GetUserSelection();
            bool includeClipping = selectObjs.GetIncludeClipping();
            bool includeHidden = selectObjs.GetIncludeHidden();

            if (objRefs == null)
            {
                return Result.Cancel;
            }

            Stopwatch watch = Stopwatch.StartNew();
            GenericMake2D createMake2D = new GenericMake2D(objRefs, currentViewport, includeClipping, includeHidden);
            HiddenLineDrawing make2D = createMake2D.GetMake2D();
            
            if (make2D == null)
            {
                return Result.Failure;
            }

            SortMake2D(doc, make2D, includeClipping, includeHidden);
            doc.Views.Redraw();

            RhinoApp.WriteLine("WeightedMake2D was Successful!");
            watch.Stop();
            long elapsedMs = watch.ElapsedMilliseconds;
            RhinoApp.WriteLine("WeightedMake2D took {0} milliseconds.", elapsedMs.ToString());

            return Result.Success;
        }
        
        /// <summary>
        /// Method used to sort HiddenLineDrawing curves based on the formal relationships 
        /// between their source edges and their adjacent faces. Creates layers for the
        /// sorted curves if they don't already exist.
        /// </summary>
        private Result SortMake2D (RhinoDoc doc, HiddenLineDrawing make2D, bool includeClipping, bool includeHidden)
        {
            //Check existance of layers
            string[] level2Lyrs = { "WT_Cut", "WT_Outline", "WT_Convex", "WT_Concave" };

            bool exists;
            Layer drawingLayer = FindOrCreateLyr(doc, "WT_Make2D", null, out exists);
            Layer visibleLayer = FindOrCreateLyr(doc, "WT_Visible", drawingLayer, out exists);
            if (includeHidden) 
            { 
                Layer hiddenLayer = FindOrCreateLyr(doc, "WT_Hidden", drawingLayer, out exists);
                hiddenLayer.Color = new ColorCMYK(0, 0, 0, 0.3);
                hiddenLayer.PlotColor = new ColorCMYK(0, 0, 0, 0.3);
            }

            int numLyrs = level2Lyrs.Length;
            double x = 1 / numLyrs;
            double y = 0.3 / numLyrs;
            double k = 0.8 / numLyrs;
            for (int i = 0; i < numLyrs; i++)
            {
                if (i == 0 && includeClipping == false) {  continue; }
                String lyrName = level2Lyrs[i];
                bool colored = false;
                Layer childLyr = FindOrCreateLyr(doc, lyrName, visibleLayer, out colored);

                if (colored == false)
                {
                    childLyr.Color = new ColorCMYK(1 - i * x, 0.3 - i * y, 0, k * i);
                    childLyr.PlotColor = childLyr.Color;
                    childLyr.PlotWeight = 0.15 * Math.Pow((numLyrs - i), 1.5);
                    int childLyrIdx = doc.Layers.Add(childLyr);
                }
            }

            // finds the layer indexes only once per run.

            int clipIdx = 0;
            int hiddenIdx = 0;
            if (includeClipping == true) { clipIdx = doc.Layers.FindName("WT_Cut").Index; }
            if (includeHidden) { hiddenIdx = doc.Layers.FindName("WT_Hidden").Index; }

            int outlineIdx = doc.Layers.FindName("WT_Outline").Index;
            int convexIdx = doc.Layers.FindName("WT_Convex").Index;
            int concaveIdx = doc.Layers.FindName("WT_Concave").Index;

            // Sorts curves into layers
            foreach (var make2DCurve in make2D.Segments)
            {
                // Check for parent curve. Discard if not found.
                if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                    continue;

                var crv = make2DCurve.CurveGeometry.DuplicateCurve();

                var attribs = new ObjectAttributes();

                // Processes visible curves
                if (crv != null && make2DCurve.SegmentVisibility == HiddenLineDrawingSegment.Visibility.Visible)
                {
                    // find midpoint, if an edge is broken up into multiple segments in the make2D, each segment
                    // is weighed individually.
                    Point3d start = crv.PointAtStart;
                    Point3d end = crv.PointAtEnd;
                    Point3d mid = start + (start - end) / 2;

                    // find source object
                    ComponentIndex ci = make2DCurve.ParentCurve.SourceObjectComponentIndex;
                    HiddenLineDrawingObject source = make2DCurve.ParentCurve.SourceObject;

                    // find source edge
                    ObjRef sourceSubObj = new ObjRef(doc, (Guid)source.Tag, ci);
                    BrepEdge sourceEdge = sourceSubObj.Edge();

                    // initialize concavity of segment
                    Concavity crvMidConcavity = Concavity.None;

                    // find concavity of original edge at segment midpoint
                    if (sourceEdge != null)
                    {
                        double sourcePt;
                        sourceEdge.ClosestPoint(mid, out sourcePt);
                        crvMidConcavity = sourceEdge.ConcavityAt(sourcePt, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    }

                    // silhouette type determines of the segment is an outline
                    SilhouetteType silType = make2DCurve.ParentCurve.SilhouetteType;

                    // sort segments into layers based on outline and concavity
                    attribs.SetUserString("SilType", silType.ToString());
                    if (silType == SilhouetteType.SectionCut)
                    {
                        if (includeClipping == false) { continue; }
                        attribs.LayerIndex = clipIdx;
                    }
                    else if (silType == SilhouetteType.Boundary || silType == SilhouetteType.Crease)
                    {
                        attribs.LayerIndex = outlineIdx;
                    }
                    else if (crvMidConcavity == Concavity.Convex)
                    {
                        attribs.LayerIndex = convexIdx;
                    }
                    else
                    {
                        attribs.LayerIndex = concaveIdx;
                    }
                }
                // process hidden curves: add them to the hidden layer
                else if (crv != null && make2DCurve.SegmentVisibility == HiddenLineDrawingSegment.Visibility.Hidden)
                {
                    if (includeHidden == false) { continue; }
                    attribs.LayerIndex = hiddenIdx;
                }

                // adds curves to document and selects them.
                Guid crvGuid = doc.Objects.AddCurve(crv, attribs);
                RhinoObject addedCrv = doc.Objects.FindId(crvGuid);
                addedCrv.Select(true);
            }

            return Result.Success;
        }

        private Layer FindOrCreateLyr (RhinoDoc doc, string lyrName, Layer parent, out bool exists)
        {
            Layer layer = doc.Layers.FindName(lyrName);
            if (layer == null)
            {
                exists = false;
                layer = new Layer();
                layer.Name = lyrName;
                if (parent != null) { layer.ParentLayerId = parent.Id; }
                doc.Layers.Add(layer);
                layer = doc.Layers.FindName(lyrName);
            }
            else { exists = true; }
            
            return layer;
        }

    }
}
