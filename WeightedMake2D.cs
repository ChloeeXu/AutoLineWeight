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
using Rhino.UI;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing;

namespace AutoLineWeight
{
    public class WeightedMake2D : Command
    {
        /// <summary>
        /// creates a weighted 2D representation of user-selected 3D geometry based
        /// on formal relationships between user-selected source geometry and its
        /// adjacent faces.
        /// </summary>

        ObjRef[] objRefs;
        Curve[] intersects;
        RhinoViewport currentViewport;
        Transform flatten;
        Transform move2D;
        Transform moveSilhouette;

        bool includeClipping = false;
        bool includeHidden = false;
        bool includeSilhouette = false;


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
            currentViewport = RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport;

            UserSelectGeometry selectObjs = new UserSelectGeometry();
            this.objRefs = selectObjs.GetUserSelection();
            this.includeClipping = selectObjs.GetIncludeClipping();
            this.includeHidden = selectObjs.GetIncludeHidden();
            this.includeSilhouette = selectObjs.GetIndluceSceneSilhouette();

            if (this.includeSilhouette) { this.includeClipping =  false; }

            if (objRefs == null)
            {
                return Result.Cancel;
            }

            Stopwatch watch0 = Stopwatch.StartNew();
            Stopwatch watch1 = Stopwatch.StartNew();

            // calculate intersections between breps
            CalculateBrepIntersects calcBrepInts = new CalculateBrepIntersects(objRefs);
            intersects = calcBrepInts.GetIntersects();

            watch1.Stop();
            RhinoApp.WriteLine("Calculating geometry intersects {0} miliseconds.", watch1.ElapsedMilliseconds.ToString());

            Stopwatch watch2 = Stopwatch.StartNew();
            // compute hiddenlinedrawing object for all the selected objects and their intersects
            GenericMake2D createMake2D = new GenericMake2D(objRefs, intersects, currentViewport, includeClipping, includeHidden);
            HiddenLineDrawing make2D = createMake2D.GetMake2D();

            if (make2D == null)
            {
                return Result.Failure;
            }

            HiddenLineDrawing intersectionMake2D = null;

            // if there are intersections between objects, compute a make2D specifically for these intersections
            if (intersects.Length != 0) 
            {
                // generate this drawing only if there are intersects
                GenericMake2D createIntersectionMake2D = new GenericMake2D(intersects, currentViewport, includeClipping, includeHidden);
                intersectionMake2D = createIntersectionMake2D.GetMake2D();

                if (intersectionMake2D == null) { return Result.Failure; }
            }

            watch2.Stop();
            RhinoApp.WriteLine("Generating Make2D {0} miliseconds.", watch2.ElapsedMilliseconds.ToString());

            Stopwatch watch3 = Stopwatch.StartNew();

            GenerateLayers(doc);

            watch3.Stop();
            RhinoApp.WriteLine("Generating Layers {0} miliseconds.", watch3.ElapsedMilliseconds.ToString());

            Stopwatch watch4 = Stopwatch.StartNew();

            SortMake2D(doc, make2D, intersectionMake2D);
            doc.Views.Redraw();

            watch4.Stop();
            RhinoApp.WriteLine("Sorting Curves took {0} miliseconds.", watch4.ElapsedMilliseconds.ToString());

            if (this.includeSilhouette)
            {
                Stopwatch watch5 = Stopwatch.StartNew();
                OutlineMake2D(doc);
                watch5.Stop();
                RhinoApp.WriteLine("Generating Outline took {0} miliseconds.", watch5.ElapsedMilliseconds.ToString());
            }

            RhinoApp.WriteLine("WeightedMake2D was Successful!");
            watch0.Stop();
            long elapsedMs = watch0.ElapsedMilliseconds;
            RhinoApp.WriteLine("WeightedMake2D took {0} milliseconds.", elapsedMs.ToString());

            return Result.Success;
        }
        
        /// <summary>
        /// Method used to sort HiddenLineDrawing curves based on the formal relationships 
        /// between their source edges and their adjacent faces. Creates layers for the
        /// sorted curves if they don't already exist.
        /// </summary>
        private Result SortMake2D (RhinoDoc doc, HiddenLineDrawing make2D, HiddenLineDrawing intersection2D)
        {
            // finds the layer indexes only once per run.
            int clipIdx = 0;
            int hiddenIdx = 0;
            if (this.includeClipping) { clipIdx = doc.Layers.FindName("WT_Cut").Index; }
            if (this.includeHidden) { hiddenIdx = doc.Layers.FindName("WT_Hidden").Index; }

            int outlineIdx = doc.Layers.FindName("WT_Outline").Index;
            int convexIdx = doc.Layers.FindName("WT_Convex").Index;
            int concaveIdx = doc.Layers.FindName("WT_Concave").Index;

            // generate intersection curves and calculate boudning box
            List<Curve> intersectionSegments = new List<Curve>();
            BoundingBox intersectionBB = new BoundingBox();
            if (intersection2D != null)
            {
                foreach (var make2DCurve in intersection2D.Segments)
                {
                    //Check for parent curve. Discard if not found.
                    if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                        continue;

                    var crv = make2DCurve.CurveGeometry.DuplicateCurve();
                    intersectionSegments.Add(crv);
                }
                intersectionBB = intersection2D.BoundingBox(false);
            }

            flatten = Transform.PlanarProjection(Plane.WorldXY);
            BoundingBox page_box = make2D.BoundingBox(true);
            var delta_2d = new Vector2d(0, 0);
            delta_2d = delta_2d - new Vector2d(page_box.Min.X, page_box.Min.Y);
            var delta_3d = Transform.Translation(new Vector3d(delta_2d.X, delta_2d.Y, 0.0));
            flatten = delta_3d * flatten;

            Point3d center = page_box.Center;
            Vector3d moveVector = new Vector3d(center);
            moveVector.Reverse();
            this.move2D = Transform.Translation(moveVector);

            // Sorts curves into layers
            foreach (var make2DCurve in make2D.Segments)
            {
                // Check for parent curve. Discard if not found.
                if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                    continue;

                var crv = make2DCurve.CurveGeometry.DuplicateCurve();

                var attribs = new ObjectAttributes();
                attribs.PlotColorSource = ObjectPlotColorSource.PlotColorFromObject;
                attribs.ColorSource = ObjectColorSource.ColorFromObject;

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

                    RhinoObject sourceObj = doc.Objects.Find((Guid)source.Tag);
                    Color objColor = sourceObj.Attributes.DrawColor(doc);
                    Color dispColor = sourceObj.Attributes.ComputedPlotColor(doc);

                    attribs.ObjectColor = objColor;
                    attribs.PlotColor = dispColor;

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

                    attribs.SetUserString("Siltype", silType.ToString());
                    // sort segments into layers based on outline and concavity
                    attribs.SetUserString("SilType", silType.ToString());
                    if (silType == SilhouetteType.SectionCut)
                    {
                        if (includeClipping == false) { continue; }
                        attribs.LayerIndex = clipIdx;
                    }
                    else if (silType == SilhouetteType.Boundary || 
                        silType == SilhouetteType.Crease || 
                        silType == SilhouetteType.Tangent || 
                        silType == SilhouetteType.TangentProjects)
                    {
                        attribs.LayerIndex = outlineIdx;
                        bool segmented = SegmentAndAddToDoc(doc, attribs, crv, intersectionSegments, intersectionBB, concaveIdx);
                        if (segmented) { continue; }
                    }
                    else if (crvMidConcavity == Concavity.Convex)
                    {
                        attribs.LayerIndex = convexIdx;
                        bool segmented = SegmentAndAddToDoc(doc, attribs, crv, intersectionSegments, intersectionBB, concaveIdx);
                        if (segmented) { continue; }
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

                    HiddenLineDrawingObject source = make2DCurve.ParentCurve.SourceObject;
                    RhinoObject sourceObj = doc.Objects.Find((Guid)source.Tag);
                    Color objColor = sourceObj.Attributes.DrawColor(doc);
                    Color dispColor = sourceObj.Attributes.ComputedPlotColor(doc);
                    attribs.ObjectColor = objColor;
                    attribs.PlotColor = dispColor;

                    attribs.LayerIndex = hiddenIdx;
                }
                else { continue; }
                // adds curves to document and selects them.
                crv.Transform(flatten);
                Guid crvGuid = doc.Objects.AddCurve(crv, attribs);
                RhinoObject addedCrv = doc.Objects.FindId(crvGuid);
                addedCrv.Select(true);
            }

            return Result.Success;
        }

        private bool SegmentAndAddToDoc (RhinoDoc doc, ObjectAttributes attribs, Curve crv, List<Curve> intersectionSegments, 
            BoundingBox intersectionBB, int concaveIdx)
        {
            if (intersectionSegments.Count == 0) { return false; }
            if (BoundingBoxCoincides(crv.GetBoundingBox(false), intersectionBB) == false) { return false; }
            CurveBooleanDifference crvBD = new CurveBooleanDifference( crv, intersectionSegments.ToArray());
            crvBD.CalculateOverlap();
            Curve[] remaining = crvBD.GetResultCurves();
            Curve[] overlap = crvBD.GetOverlapCurves();

            foreach(Curve remainingCrv in remaining)
            {
                remainingCrv.Transform(flatten);
                attribs.SetUserString("LayerIdx", attribs.LayerIndex.ToString());
                Guid crvGuid = doc.Objects.AddCurve(remainingCrv, attribs);
                RhinoObject addedCrv = doc.Objects.FindId(crvGuid);
                addedCrv.Select(true);
            }
            attribs.LayerIndex = concaveIdx;
            foreach (Curve overlappingCrv in overlap)
            {
                overlappingCrv.Transform(flatten);
                attribs.SetUserString("LayerIdx", attribs.LayerIndex.ToString());
                Guid crvGuid = doc.Objects.AddCurve(overlappingCrv, attribs);
                RhinoObject addedCrv = doc.Objects.FindId(crvGuid);
                addedCrv.Select(true);
            }

            return true;
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

        private void GenerateLayers(RhinoDoc doc)
        {
            // Check existance of layers
            string[] level2Lyrs = { "WT_Cut", "WT_Silhouette", "WT_Outline", "WT_Convex", "WT_Concave" };

            // Find the layers if they exist, create them if not
            bool exists;
            Layer drawingLayer = FindOrCreateLyr(doc, "WT_Make2D", null, out exists);
            Layer visibleLayer = FindOrCreateLyr(doc, "WT_Visible", drawingLayer, out exists);
            // Does not create hidden layer if hidden lines not requested
            if (includeHidden)
            {
                Layer hiddenLayer = FindOrCreateLyr(doc, "WT_Hidden", drawingLayer, out exists);
                if (exists == false)
                {
                    int ltIdx = doc.Linetypes.Find("Hidden");
                    if (ltIdx >= 0)
                    {
                        hiddenLayer.LinetypeIndex = ltIdx;
                    }
                    hiddenLayer.PlotWeight = 0.1;
                }
            }

            // Assigns lineweight to layers
            int numLyrs = level2Lyrs.Length;
            double x = 1 / numLyrs;
            double y = 0.3 / numLyrs;
            double k = 0.8 / numLyrs;
            for (int i = 0; i < numLyrs; i++)
            {
                if (i == 0 && includeClipping == false) { continue; }
                if (i == 1 && includeSilhouette == false) { continue; }
                String lyrName = level2Lyrs[i];
                bool colored = false;
                Layer childLyr = FindOrCreateLyr(doc, lyrName, visibleLayer, out colored);

                if (colored == false)
                {
                    childLyr.PlotWeight = 0.15 * Math.Pow((numLyrs - i), 1.5);
                }
            }
        }

        private void OutlineMake2D(RhinoDoc doc)
        {
            MeshOutline outliner = new MeshOutline(objRefs, currentViewport);
            PolylineCurve[] outlines = outliner.GetOutlines();
            GenericMake2D outline2DMaker = new GenericMake2D(outlines, currentViewport, includeClipping, includeHidden);
            HiddenLineDrawing outline2D = outline2DMaker.GetMake2D();

            if (outline2D == null)
            {
                return;
            }

            BoundingBox silhouetteBB = outline2D.BoundingBox(false);
            Point3d center = silhouetteBB.Center;
            Vector3d moveVector = new Vector3d(center);
            moveVector.Reverse();
            this.moveSilhouette = Transform.Translation(moveVector);

            foreach (var make2DCurve in outline2D.Segments)
            {
                // Check for parent curve. Discard if not found.
                if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                    continue;

                var crv = make2DCurve.CurveGeometry.DuplicateCurve();

                var attribs = new ObjectAttributes();
                attribs.PlotColorSource = ObjectPlotColorSource.PlotColorFromObject;
                attribs.ColorSource = ObjectColorSource.ColorFromObject;
                attribs.LayerIndex = doc.Layers.FindName("WT_Silhouette").Index;

                crv.Transform(moveSilhouette);

                Guid crvGuid = doc.Objects.Add(crv, attribs);
                RhinoObject addedCrv = doc.Objects.FindId(crvGuid);
                addedCrv.Select(true);
            }
        }

        // Calculates whether two bounding boxes overlap (this is repeated in three classes, simplify?)
        private bool BoundingBoxCoincides(BoundingBox bb1, BoundingBox bb2)
        {
            return bb1.Min.X <= bb2.Max.X &&
                bb1.Max.X >= bb2.Min.X &&
                bb1.Min.Y <= bb2.Max.Y &&
                bb1.Max.Y >= bb2.Min.Y &&
                bb1.Min.Z <= bb2.Max.Z &&
                bb1.Max.Z >= bb2.Min.Z;
        }
    }
}
