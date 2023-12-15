using System;
using System.Runtime.Remoting;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;

namespace AutoLineWeight
{
    public class Tester : Command
    {
        public Tester()
        {
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static Tester Instance { get; private set; }

        public override string EnglishName => "Tester";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            TestCurveBooleanDifference(doc, mode);
            return Result.Success;
        }

        private Result TestCurveBooleanDifference (RhinoDoc doc, RunMode mode)
        {
            SimpleSelect selecter1 = new SimpleSelect("tester to difference from.");
            SimpleSelect selecter2 = new SimpleSelect("tester to difference with.");
            Rhino.DocObjects.ObjRef[] selection1 = selecter1.GetSimpleSelection();
            Rhino.DocObjects.ObjRef[] selection2 = selecter2.GetSimpleSelection();
            Curve[] crvsel1 = new Curve[selection1.Length];
            Curve[] crvsel2 = new Curve[selection2.Length];
            for (int i = 0; i < selection1.Length; i++)
            {
                crvsel1[i] = selection1[i].Curve().DuplicateCurve();
            }
            for (int i = 0; i < selection2.Length; i++)
            {
                crvsel2[i] = selection2[i].Curve().DuplicateCurve();
            }

            CurveBooleanDifference testdiff = new CurveBooleanDifference(crvsel1, crvsel2);
            Curve[] results = testdiff.GetResultCurves();

            foreach (Curve crv in results)
            {
                doc.Objects.Add(crv);
            }

            return Result.Success;
        }
    }
}