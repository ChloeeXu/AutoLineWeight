using System;
using Rhino;
using Rhino.Commands;

namespace AutoLineWeight
{
    public class ProjectedIntersect : Command
    {
        public ProjectedIntersect()
        {
            Instance = this;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static ProjectedIntersect Instance { get; private set; }

        public override string EnglishName => "ProjectedIntersect";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.
            return Result.Success;
        }
    }
}