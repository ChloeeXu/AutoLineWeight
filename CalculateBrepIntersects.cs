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

                    if (!BoundingBoxCoincides(bb1, bb2)) {  continue; }

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

        private bool BoundingBoxCoincides(BoundingBox bb1, BoundingBox bb2)
        {
            return bb1.Min.X <= bb2.Max.X &&
                bb1.Max.X >= bb2.Min.X &&
                bb1.Min.Y <= bb2.Max.Y &&
                bb1.Max.Y >= bb2.Min.Y &&
                bb1.Min.Z <= bb2.Max.Z &&
                bb1.Max.Z >= bb2.Min.Z;
        }

        private List<Interval> MergeOverlappingIntervals(List<Interval> intervals)
        {
            if (intervals.Count <= 1)
            {
                return intervals;
            }

            intervals.Sort((x, y) => x.Min.CompareTo(y.Min));

            List<Interval> mergedIntervals = new List<Interval> { intervals[0] };

            for (int i = 1; i < intervals.Count; i++)
            {
                Interval current = intervals[i];
                Interval previous = mergedIntervals[mergedIntervals.Count - 1];

                if (current.Min <= previous.Max)
                {
                    mergedIntervals[mergedIntervals.Count - 1] = new Interval(previous.Min, Math.Max(previous.Max, current.Max));
                }
                else
                {
                    mergedIntervals.Add(current);
                }
            }

            return mergedIntervals;
        }

        public Curve[] GetIntersects()
        {
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
            return this.intersects.ToArray();
        }
    }
}