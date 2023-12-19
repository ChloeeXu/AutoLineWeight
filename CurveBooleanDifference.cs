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
        // initialize curve selection
        Curve fromCurve;
        Curve[] withCurves;

        List<Curve> resultCurves = new List<Curve>();
        List<Curve> overlaps = new List<Curve>();

        public CurveBooleanDifference(Curve fromCurve, Curve[] withCurves)
        {
            this.fromCurve = fromCurve;
            this.withCurves = withCurves;
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static CurveBooleanDifference Instance { get; private set; }

        public override string EnglishName => "CurveBooleanDifference";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (fromCurve == null) { return Result.Cancel; }

            BoundingBox bb1 = fromCurve.GetBoundingBox(false);

            double startParam;
            fromCurve.LengthParameter(0, out startParam);
            double endParam;
            fromCurve.LengthParameter(fromCurve.GetLength(), out endParam);

            Interval fromInterval = new Interval(startParam, endParam);

            List<Interval> remainingIntervals = new List<Interval> { fromInterval };
            List<Interval> overlapIntervals = new List<Interval>();

            foreach (Curve withCurve in withCurves)
            {
                if (withCurve == null) { continue; }

                BoundingBox bb2 = withCurve.GetBoundingBox(false);
                if (!BoundingBoxCoincides(bb1, bb2)) { continue; }

                double tol = doc.ModelAbsoluteTolerance;
                CurveIntersections intersections = Intersection.CurveCurve(fromCurve, withCurve, tol, tol);

                for (int i = 0; i < intersections.Count; i++)
                {
                    IntersectionEvent intersection = intersections[i];
                    if (intersection.IsOverlap)
                    {
                        Interval overlap = intersection.OverlapA;
                        overlapIntervals.Add(overlap);
                        remainingIntervals = IntervalDifference(remainingIntervals, overlap);
                    }
                }
            }

            foreach (Interval interval in remainingIntervals)
            {
                Curve trimmed = fromCurve.Trim(interval);
                resultCurves.Add(trimmed);
            }

            List<Interval> cleanedOverlaps = MergeOverlappingIntervals(overlapIntervals);

            foreach (Interval interval in cleanedOverlaps)
            {
                Curve trimmed = fromCurve.Trim(interval);
                overlaps.Add(trimmed);
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

        public void CalculateOverlap()
        {
            RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
        }

        public Curve[] GetResultCurves()
        {
            return this.resultCurves.ToArray();
        }

        public Curve[] GetOverlapCurves()
        {
            return this.overlaps.ToArray();
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
    }
}