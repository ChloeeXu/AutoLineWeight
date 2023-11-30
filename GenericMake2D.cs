/*
------------------------------

This class use used to create a flattened HiddenLineDrawing (make2d) of 
input 3D geometry. It does not process the drawing in any way after 
creation. Could be further developed to include more HiddenLineDrawing 
options.

------------------------------
created 11/29/2023
Ennead Architects

Chloe Xu
chloe.xu@ennead.com
edited:11/30/2023

------------------------------
*/

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
        private HiddenLineDrawing resultMake2D;

        /// <summary>
        /// Creates a HiddenLineDrawing of input geometry from the input
        /// viewport.
        /// </summary>
        /// <param name="objRefs"> Array of ObjRef. Objects to be processed into 2d drawing. </param>
        /// <param name="viewport"> Viewport from which to make the drawing.</param>
        public GenericMake2D(ObjRef[] objRefs, RhinoViewport viewport)
        {
            Instance = this;
            this.toMake2D = objRefs;
            this.currentViewport = viewport;
        }

        ///<summary>The only instance of the MyCommand command.</summary>
        public static GenericMake2D Instance { get; private set; }

        public override string EnglishName => "GenericMake2D";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            HiddenLineDrawingParameters make2DParams = new HiddenLineDrawingParameters();
            make2DParams.SetViewport(this.currentViewport);
            make2DParams.IncludeHiddenCurves = true;
            make2DParams.IncludeTangentEdges = true;
            make2DParams.Flatten = true;
            make2DParams.AbsoluteTolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            if (this.currentViewport == null)
            {
                return Result.Failure;
            }

            foreach (ObjRef objRef in this.toMake2D)
            {
                RhinoObject obj = objRef?.Object();
                if (obj != null) { make2DParams.AddGeometry(obj.Geometry, Transform.Identity, obj.Id); }
            }

            HiddenLineDrawing make2D = HiddenLineDrawing.Compute(make2DParams, true);
            this.resultMake2D = make2D;
            return Result.Success;
        }

        /// <summary>
        /// Processes and gets the resultant HiddenLineDrawing
        /// </summary>
        /// <returns> A HiddenLineDrawing. Tags on the HiddenLineDrawingObject from the drawing 
        /// are object ids of their sources</returns>
        public HiddenLineDrawing GetMake2D()
        {
            this.RunCommand(RhinoDoc.ActiveDoc, RunMode.Interactive);
            return this.resultMake2D;
        }
    }
}