using System;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.DocObjects.Custom;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace AutoLineWeight
{
    public class CalulateBrepIntersects : Command
    {
        Rhino.DocObjects.ObjRef[] objSel;
        Curve[] intersects;
        public CalulateBrepIntersects(Rhino.DocObjects.ObjRef[] sourceObjSel)
        {
            Instance = this;
            this.objSel = sourceObjSel;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static CalulateBrepIntersects Instance { get; private set; }

        public override string EnglishName => "CalulateIntersects";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            int len = objSel.Length;
            for (int i = 0; i < len; i++)
            {
                Rhino.DocObjects.ObjRef obj1 = objSel[i];
                Brep brep1 = obj1.Brep();
                if (brep1 == null) { continue; }

                for (int j = i + 1; j < len; j++)
                {
                    Rhino.DocObjects.ObjRef obj2 = objSel[j];
                    Brep brep2 = obj2.Brep();
                    if (brep2 == null) { continue; }

                    BoundingBox bb1 = obj1.Object().Geometry.GetBoundingBox(false);
                    BoundingBox bb2 = obj2.Object().Geometry.GetBoundingBox(false);

                    if (!BoundingBoxCoincides(bb1, bb2)) {  continue; }

                    Point3d[] ptIntersect;
                    Curve[] crvIntersect;
                    double tol = doc.ModelAbsoluteTolerance;
                    bool success = Intersection.BrepBrep(brep1, brep2, tol, out crvIntersect, out ptIntersect);
                    if (!success) { continue; }
                    foreach (Curve crv in crvIntersect)
                    {
                        crv.SetUserString("ParentObj", obj1.ObjectId.ToString());
                        
                    }
                }
            }
            return Result.Success;
        }

        private bool BoundingBoxCoincides(BoundingBox bb1, BoundingBox bb2)
        {
            return bb1.Min.X < bb2.Max.X &&
                bb1.Max.X > bb2.Min.X &&
                bb1.Min.Y < bb2.Max.Y &&
                bb1.Max.Y > bb2.Min.Y &&
                bb1.Min.Z < bb2.Max.Z &&
                bb1.Max.Z > bb2.Min.Z;
        }
    }
}