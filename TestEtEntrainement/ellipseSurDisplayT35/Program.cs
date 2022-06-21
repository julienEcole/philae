using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using Microsoft.SPOT.Input;

namespace GadgeteerApp1
{
    public partial class Program
    {
        Joystick.Position position;
          
        void ProgramStarted()
        {
            position = new Joystick.Position() ;
            display.SimpleGraphics.DisplayText("Tracer des ellipses",Resources.GetFont(Resources.FontResources.NinaB),GT.Color.Cyan,0,0);
            joystick.JoystickPressed +=new Joystick.JoystickEventHandler(joystick_JoystickPressed);
        }

        void  joystick_JoystickPressed(Joystick sender, Joystick.JoystickState state)
        {
            uint x;
            uint y;
            position = joystick.GetPosition();
            if (position.X < 0) position.X *= -1;
            if (position.Y < 0) position.Y *= -1;
            x=(uint)(position.X*140);
            y=(uint)(position.Y*100);
 	        display.SimpleGraphics.DisplayEllipse(GT.Color.Blue,160,120,x,y);
        } 
    }  
}
