using System;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace AutoLineWeight
{
    public class GenericMake2D : Command
    {
        private RhinoViewport currentViewport;        
        public GenericMake2D(RhinoViewport viewport)
        {
            Instance = this;
            currentViewport = viewport;
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static GenericMake2D Instance { get; private set; }

        public override string EnglishName => "GenericMake2D";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            HiddenLineDrawingParameters genericDrawingParams = new HiddenLineDrawingParameters();
            genericDrawingParams.SetViewport(currentViewport);
            genericDrawingParams.IncludeHiddenCurves = false;
            genericDrawingParams.AbsoluteTolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            RhinoDoc activeDoc = RhinoDoc.ActiveDoc;

            // Set up the object selection options
            GetObject getObjectOptions = new GetObject();
            getObjectOptions.GeometryFilter = ObjectType.Surface | ObjectType.PolysrfFilter;
            getObjectOptions.SubObjectSelect = true;

            // Prompt the user to select a polysurface
            RhinoApp.WriteLine("Please select geometry for the weighted make2d.");
            GetResult getResult = getObjectOptions.Get();

            // Check if the user made a selection
            if (getResult == GetResult.Object)
            {
                // Get the selected object
                RhinoObject selectedObject = getObjectOptions.Object(0).Object();

                if (selectedObject != null && selectedObject.Geometry is Brep brep)
                {
                    // Do something with the selected polysurface (Brep)
                    RhinoApp.WriteLine("Selected Polysurface: " + brep.ToString());
                }
            }

            return Result.Success;
        }
    }
}