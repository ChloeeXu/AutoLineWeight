/*
------------------------------

This command creates a weighted 2D representation of user-selected 3D geometry.
It weighs the 2D lines based on formal relationships between the source geometry
edges and its adjacent faces. If an edge only has one adjacent face, or one of
its two adjacent faces is hidden, it is defined as an "outline". If both faces are
present and the line is on a convex corner, it is defined as "convex". All other
visible lines are defined as "concave". Hidden lines are also processed. Results
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

            if (objRefs == null)
            {
                return Result.Cancel;
            }

            GenericMake2D createMake2D = new GenericMake2D(objRefs, currentViewport);
            HiddenLineDrawing make2D = createMake2D.GetMake2D();
            
            if (make2D == null)
            {
                return Result.Failure;
            }

            SortMake2D(doc, make2D);
            doc.Views.Redraw();

            RhinoApp.WriteLine("WeightedMake2D was Successful!");
            RhinoApp.WriteLine("Objects that remain selected were not processed.");

            return Result.Success;
        }
        
        /// <summary>
        /// Method used to sort HiddenLineDrawing curves based on the formal relationships 
        /// between their source edges and their adjacent faces. Creates layers for the
        /// sorted curves if they don't already exist.
        /// </summary>
        private Result SortMake2D (RhinoDoc doc, HiddenLineDrawing make2D)
        {
            //Check existance of layers
            Layer drawingLayer = doc.Layers.FindName("Weighted_Make2D");

            if (drawingLayer == null)
            {
                String[] level1Lyrs = { "Weighted_Visible", "Weighted_Hidden" };
                String[] level2Lyrs = { "Cut", "Outline", "Convex", "Concave" };

                // Create parent layer for entire drawing
                drawingLayer = new Layer();
                drawingLayer.Name = "Weighted_Make2D";
                int dwgLyrIdx = doc.Layers.Add(drawingLayer);
                drawingLayer = doc.Layers.FindName("Weighted_Make2D");

                // Create sublayer for visible and hidden curves
                foreach (String lyrName in level1Lyrs)
                {
                    Layer childLyr = new Layer();
                    childLyr.ParentLayerId = drawingLayer.Id;
                    childLyr.Name = lyrName;
                    int childLyrIdx = doc.Layers.Add(childLyr);
                }

                // Create sublayers under visible curves for weights representing
                // formal relationship.
                Layer visibleLyr = doc.Layers.FindName("Weighted_Visible");
                int numLyrs = level2Lyrs.Length;
                for (int i = 0; i < numLyrs; i++)
                {
                    String lyrName = level2Lyrs[i];
                    Layer childLyr = new Layer();
                    childLyr.ParentLayerId = visibleLyr.Id;
                    childLyr.Name = lyrName;

                    // Customizes layer display based on weights.
                    double x = 1 / numLyrs;
                    double y = 0.3 / numLyrs;
                    double k = 0.8 / numLyrs;
                    childLyr.Color = new ColorCMYK(1 - i * x, 0.3 - i * y, 0, k * i);
                    childLyr.PlotColor = childLyr.Color;
                    childLyr.PlotWeight = 0.15 * Math.Pow((numLyrs - i), 1.5);
                    int childLyrIdx = doc.Layers.Add(childLyr);
                }

                Layer hiddenLyr = doc.Layers.FindName("Weighted_Hidden");
                hiddenLyr.Color = new ColorCMYK(0, 0, 0, 0.3);
            }

            // finds the layer indexes only once per run.
            int clipIdx = doc.Layers.FindName("Cut").Index;
            int outlineIdx = doc.Layers.FindName("Outline").Index;
            int convexIdx = doc.Layers.FindName("Convex").Index;
            int concaveIdx = doc.Layers.FindName("Concave").Index;
            int hiddenIdx = doc.Layers.FindName("Weighted_Hidden").Index;

            // Sorts curves into layers
            foreach (var make2DCurve in make2D.Segments)
            {
                // Check for parent curve. Discard if not found.
                if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                    continue;

                var crv = make2DCurve.CurveGeometry.DuplicateCurve();

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
                    var attribs = new ObjectAttributes();
                    attribs.SetUserString("SilType", silType.ToString());
                    if (silType == SilhouetteType.SectionCut)
                    {
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

                    // add segments to file
                    doc.Objects.AddCurve(crv, attribs);
                }
                // process hidden curves: add them to the hidden layer
                else if (crv != null && make2DCurve.SegmentVisibility == HiddenLineDrawingSegment.Visibility.Hidden)
                {
                    var attribs = new ObjectAttributes();
                    attribs.LayerIndex = hiddenIdx;
                    doc.Objects.AddCurve(crv, attribs);
                }

            }
            return Result.Success;
        }

    }
}
