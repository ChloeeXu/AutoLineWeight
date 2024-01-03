using System;
using System.Collections.Generic;
using System.Linq;
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
    public class CalculateBrepIntersects : Command
    {
        Rhino.DocObjects.ObjRef[] objSel;
        List<Curve> intersects = new List<Curve>();
        public CalculateBrepIntersects(Rhino.DocObjects.ObjRef[] sourceObjSel)
        {
            Instance = this;
            this.objSel = sourceObjSel;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static CalculateBrepIntersects Instance { get; private set; }

        public override string EnglishName => "CalculateBrepIntersects";

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

                    if (!BoundingBoxOperations.BoundingBoxCoincides(bb1, bb2)) {  continue; }

                    Point3d[] ptIntersect;
                    Curve[] crvIntersect;
                    double tol = doc.ModelAbsoluteTolerance;
                    bool success = Intersection.BrepBrep(brep1, brep2, tol, out crvIntersect, out ptIntersect);
                    if (!success) { continue; }
                    foreach (Curve crv in crvIntersect)
                    {
                        if (crv == null) { continue; }
                        crv.UserDictionary.Set("parentObj1", obj1.ObjectId);
                        crv.UserDictionary.Set("parentObj2", obj2.ObjectId);
                        this.intersects.Add(crv);
                    }
                }
            }
            return Result.Success;
        }

        public Curve[] GetIntersects()
        {
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
            return this.intersects.ToArray();
        }
    }
}