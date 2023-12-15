using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace AutoLineWeight
{
    public class CurveBooleanDifference : Command
    {
        Curve[] fromCurves;
        Curve[] withCurves;

        List<Curve> resultCurves = new List<Curve>();

        public CurveBooleanDifference(Curve[] fromCurves, Curve[] withCurves)
        {
            this.fromCurves = fromCurves;
            this.withCurves = withCurves;
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static CurveBooleanDifference Instance { get; private set; }

        public override string EnglishName => "CurveBooleanDifference";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            foreach (Curve fromCurve in fromCurves)
            {
                if (fromCurve == null) { continue; }

                BoundingBox bb1 = fromCurve.GetBoundingBox(false);

                double startParam;
                fromCurve.LengthParameter(0, out startParam);
                double endParam;
                fromCurve.LengthParameter(fromCurve.GetLength(), out endParam);

                Interval fromInterval = new Interval(startParam, endParam);

                List<Interval> remainingIntervals = new List<Interval> { fromInterval };

                foreach (Curve withCurve in withCurves)
                {
                    if (withCurve == null) { continue; }

                    BoundingBox bb2 = withCurve.GetBoundingBox(false);
                    if (!BoundingBoxCoincides(bb1, bb2)) {  continue; }

                    double tol = doc.ModelAbsoluteTolerance;
                    CurveIntersections intersections = Intersection.CurveCurve(fromCurve, withCurve, tol, tol);

                    for (int i = 0; i < intersections.Count; i++)
                    {
                        IntersectionEvent intersection = intersections[i];
                        if (intersection.IsOverlap)
                        {
                            Interval overlap = intersection.OverlapA;
                            remainingIntervals = IntervalDifference(remainingIntervals, overlap);
                        }
                    }
                }

                foreach (Interval interval in remainingIntervals)
                {
                    Curve trimmed = fromCurve.Trim(interval);
                    resultCurves.Add(trimmed);
                }
            }
            // TODO: complete command.
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

        private List<Interval> IntervalDifference(List<Interval> intervals, Interval toRemove)
        {
            List<Interval> remaining = new List<Interval>();
            for(int i = 0; i < intervals.Count; i++)
            {
                Interval interval = intervals[i];
                interval.MakeIncreasing();

                if (interval.Min >= toRemove.Max || interval.Max <= toRemove.Min)
                {
                    remaining.Add(interval);
                    continue;
                }
                if (interval.Min < toRemove.Min)
                {
                    remaining.Add(new Interval(interval.Min, Math.Min(toRemove.Min, interval.Max)));
                }

                if (interval.Max > toRemove.Max)
                {
                    remaining.Add(new Interval(Math.Max(toRemove.Max, interval.Min), interval.Max));
                }
            }
            return remaining;
        }

        public Curve[] GetResultCurves()
        {
            RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
            return this.resultCurves.ToArray();
        }

        //public void Test ()
        //{
        //    Interval test1 = new Interval(1, 7);
        //    Interval test2 = new Interval(3, 5);
        //    Interval test3 = new Interval(1, 5);
        //    Interval test4 = new Interval(2, 8);
        //    Interval test5 = new Interval(7, 9);
        //    Interval toRemove = new Interval(2, 6);

        //    List<Interval> testIntervals = new List<Interval>
        //    {
        //        test1, test2, test3, test4, test5
        //    };

        //    List<Interval> testResults = IntervalDifference(testIntervals, toRemove);

        //    RhinoApp.WriteLine(testResults.ToString());
        //}
    }
}