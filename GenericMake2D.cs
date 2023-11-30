using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;

namespace AutoLineWeight
{
    public class GenericMake2D : Command
    {
        private RhinoViewport currentViewport;
        private ObjRef[] toMake2D;
        public GenericMake2D(ObjRef[] objRefs, RhinoViewport viewport)
        {
            Instance = this;
            toMake2D = objRefs;
            currentViewport = viewport;
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static GenericMake2D Instance { get; private set; }

        public override string EnglishName => "GenericMake2D";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Make2D(doc, mode);
            return Result.Success;
        }

        private Result Make2D (RhinoDoc doc, RunMode mode)
        {
            RhinoDoc activeDoc = RhinoDoc.ActiveDoc;

            HiddenLineDrawingParameters make2DParams = new HiddenLineDrawingParameters();
            make2DParams.SetViewport(currentViewport);
            make2DParams.IncludeHiddenCurves = true;
            make2DParams.IncludeTangentEdges = true;
            make2DParams.Flatten = true;
            make2DParams.AbsoluteTolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            if (currentViewport == null)
            {
                return Result.Failure;
            }

            foreach (ObjRef objRef in toMake2D)
            {
                RhinoObject obj = objRef?.Object();
                if (obj != null) { make2DParams.AddGeometry(obj.Geometry, Transform.Identity, obj.Id); }
            }

            HiddenLineDrawing make2D = HiddenLineDrawing.Compute(make2DParams, true);

            if (make2D != null)
            {
                // Creates layers to store objects if they don't yet exist.
                Layer drawingLayer = doc.Layers.FindName("Weighted_Make2D");
                
                if (drawingLayer == null)
                {
                    String[] level1Lyrs = { "Weighted_Visible", "Weighted_Hidden" };
                    String[] level2Lyrs = { "Outline", "Convex", "Concave" };
                    drawingLayer = new Layer();
                    drawingLayer.Name = "Weighted_Make2D";
                    int dwgLyrIdx = doc.Layers.Add(drawingLayer);
                    drawingLayer = doc.Layers.FindName("Weighted_Make2D");

                    foreach (String lyrName in level1Lyrs)
                    {
                        Layer childLyr = new Layer();
                        childLyr.ParentLayerId = drawingLayer.Id;
                        childLyr.Name = lyrName;
                        int childLyrIdx = doc.Layers.Add(childLyr);
                    }

                    Layer visibleLyr = doc.Layers.FindName("Weighted_Visible");
                    int numLyrs = level2Lyrs.Length;
                    for (int i = 0; i < numLyrs; i++)
                    {
                        String lyrName = level2Lyrs[i];
                        Layer childLyr = new Layer();
                        childLyr.ParentLayerId = visibleLyr.Id;
                        childLyr.Name = lyrName;
                        double x = 1 / numLyrs;
                        double y = 0.3/numLyrs;
                        double k = 0.8 / numLyrs;
                        childLyr.Color = new ColorCMYK(1 - i * x, 0.3 - i * y, 0, k * i);
                        childLyr.PlotColor = childLyr.Color;
                        childLyr.PlotWeight = 0.1 * Math.Pow((numLyrs - i), 1.5);
                        int childLyrIdx = doc.Layers.Add(childLyr);
                    }

                    Layer hiddenLyr = doc.Layers.FindName("Weighted_Hidden");
                    hiddenLyr.Color = new ColorCMYK(0, 0, 0, 0.3);
                }

                // finds the layer indexes only once per run.
                int outlineIdx = doc.Layers.FindName("Outline").Index;
                int convexIdx = doc.Layers.FindName("Convex").Index;
                int concaveIdx = doc.Layers.FindName("Concave").Index;
                int hiddenIdx = doc.Layers.FindName("Weighted_Hidden").Index;

                foreach (var make2DCurve in make2D.Segments)
                {
                    if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                        continue;

                    var crv = make2DCurve.CurveGeometry.DuplicateCurve();

                    if (crv != null && make2DCurve.SegmentVisibility == HiddenLineDrawingSegment.Visibility.Visible)
                    {
                        Point3d start = crv.PointAtStart;
                        Point3d end = crv.PointAtEnd;
                        Point3d mid = start + (start - end) / 2;

                        ComponentIndex ci = make2DCurve.ParentCurve.SourceObjectComponentIndex;
                        HiddenLineDrawingObject source = make2DCurve.ParentCurve.SourceObject;

                        ObjRef sourceSubObj = new ObjRef(doc, (Guid)source.Tag, ci);
                        BrepEdge sourceEdge = sourceSubObj.Edge();

                        string subObjType = "other";
                        Concavity crvMidConcavity = Concavity.None;

                        if (sourceEdge != null)
                        {
                            double sourcePt;
                            sourceEdge.ClosestPoint(mid, out sourcePt);
                            crvMidConcavity = sourceEdge.ConcavityAt(sourcePt, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        }

                        SilhouetteType silType = make2DCurve.ParentCurve.SilhouetteType;

                        var attribs = new ObjectAttributes();
                        attribs.SetUserString("Silhouette type: ", silType.ToString());
                        attribs.SetUserString("Source subobject type", subObjType);

                        if (silType == SilhouetteType.Boundary || silType == SilhouetteType.Crease)
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

                        doc.Objects.AddCurve(crv, attribs);
                    }
                    else if (crv != null && make2DCurve.SegmentVisibility == HiddenLineDrawingSegment.Visibility.Hidden)
                    {
                        var attribs = new ObjectAttributes();
                        attribs.LayerIndex = hiddenIdx;
                        doc.Objects.AddCurve(crv, attribs);
                    }
                }
            }
            doc.Views.Redraw();

            return Result.Success;
        }
    }
}