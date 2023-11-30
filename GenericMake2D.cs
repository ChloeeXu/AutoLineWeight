using System;
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
                //var flatten = Transform.PlanarProjection(Plane.WorldXY);
                //BoundingBox page_box = make2D.BoundingBox(true);
                //var delta_2d = new Vector2d(0, 0);
                //delta_2d = delta_2d - new Vector2d(page_box.Min.X, page_box.Min.Y);
                //var delta_3d = Transform.Translation(new Vector3d(delta_2d.X, delta_2d.Y, 0.0));
                //flatten = delta_3d * flatten;

                foreach (var make2DCurve in make2D.Segments)
                {
                    if (make2DCurve?.ParentCurve == null || make2DCurve.ParentCurve.SilhouetteType == SilhouetteType.None)
                        continue;

                    var crv = make2DCurve.CurveGeometry.DuplicateCurve();
                    Point3d start = crv.PointAtStart;
                    Point3d end = crv.PointAtEnd;
                    Point3d mid = start + (start - end) / 2;

                    ComponentIndex ci = make2DCurve.ParentCurve.SourceObjectComponentIndex;
                    HiddenLineDrawingObject source = make2DCurve.ParentCurve.SourceObject;

                    ObjRef sourceSubObj = new ObjRef(doc, (Guid)source.Tag, ci);
                    BrepEdge sourceEdge = sourceSubObj.Edge();

                    string subObjType = "other";

                    if (sourceEdge != null)
                    {
                        double sourcePt;
                        sourceEdge.ClosestPoint(mid, out sourcePt);
                        Concavity concavityAt = sourceEdge.ConcavityAt(sourcePt, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        switch (concavityAt)
                        {
                            case Concavity.Convex:
                                subObjType = "convex";
                                break;
                            case Concavity.Concave:
                                subObjType = "concave";
                                break;
                        }
                    }

                    var attribs = new ObjectAttributes { Name = "V" };
                    attribs.SetUserString("Silhouette type: ", make2DCurve.ParentCurve.SilhouetteType.ToString());
                    attribs.SetUserString("Source subobject type", subObjType);

                    if (crv != null && make2DCurve.SegmentVisibility == HiddenLineDrawingSegment.Visibility.Visible)
                    {
                        //crv.Transform(flatten);
                        doc.Objects.AddCurve(crv, attribs);
                    }
                }
            }
            doc.Views.Redraw();

            return Result.Success;
        }
    }
}