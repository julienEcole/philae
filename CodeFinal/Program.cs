//version du 30 mai 2022 JR
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//**************************************************************************************************************************//
//*                                                3FF->300°, 0x00->0°                                                      //
//* byte[] trame = new byte[] { 0xFF, 0xFF, 0X01, 0x05, 0x03, 0x1E, 0x00, 0x02, 0xD6 }; Retour au centre                    //
//* byte[] TabAngle = new byte[] { 0xFF, 0xFF, 0X01, 0x05, 0x03, 0x1E, 0xFF, 0x03, 0xD6 };  0xEAF%256 Retour position final //
//*                                                                                                                         //
//**************************************************************************************************************************//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;
using Microsoft.SPOT;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GTI = Gadgeteer.Interfaces;
using GHI.Premium.Hardware;
using Gadgeteer.Interfaces;

namespace GadgeteerApp1
{   public partial class Program
    {  bool TEST = true;  //false pour la version finale
       int ETATMUPUS = 0;//bit B0=déployé, B1=vibre, B2=laché
       int ETATROMAP = 0;//0 à 100% du déploiement
       bool MUPUSDEPLOYE = false;
       bool VIBRE = false;
       bool LACHE = false;
       int AngleBasRomap = 210;//220;     //223? position initiale=219 
       int AngleHautRomap = 125;//129;    //position finale=150//143
       
       Gadgeteer.Interfaces.Serial serie;
       //GT.Interfaces.DigitalIO onGPIO;
       GT.Interfaces.PWMOutput S8P8pwmMarteau;
       GHI.Premium.Hardware.CAN m_busCan;
       GHI.Premium.Hardware.CAN.Message[] m_data = new GHI.Premium.Hardware.CAN.Message[1];
       GT.Interfaces.DigitalOutput S11P3dirAx12,S8P7ventouse,S8P6lacher;

       
       int appui = 0, mode=0;   //bleu vert rouge : 0=Joystick 1=CAN 2=USB
       int action;
        // This method is run when the mainboard is powered up or reset.   
        /*  Socket Type C
            Controller-area network (CAN, or CAN-Bus). Pins number 4 and 5 serve as the CAN transmit (TD) and receive (RD) pins, and double as general-purpose input/outputs. 
            In addition, pins number 3 abd 6 are general-purpose input/outputs, and pin number 3 supports interrupt capabilities.
         */

        void ProgramStarted()
       {
            S8P7ventouse = extender2.SetupDigitalOutput(GT.Socket.Pin.Seven, false);
            S8P6lacher = extender2.SetupDigitalOutput(GT.Socket.Pin.Six, false);
            S8P8pwmMarteau = extender2.SetupPWMOutput(GT.Socket.Pin.Eight); S8P8pwmMarteau.Set(1000, 0.001);
            //S12P4effethall = extender3.SetupDigitalInput(GT.Socket.Pin.Four, GlitchFilterMode.Off, ResistorMode.Disabled);
            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            if (TEST) Debug.Print("Version du 31 mai 2022");

            // Communication USB
            usbSerial.Configure(9600, GT.Interfaces.Serial.SerialParity.None, GT.Interfaces.Serial.SerialStopBits.One, 8);
            usbSerial.SerialLine.Open();
            usbSerial.SerialLine.Write("--Philea--\n\r");
            usbSerial.SerialLine.Write("0 Deploiement MUPUS\n\r");
            usbSerial.SerialLine.Write("1 Deploiement ROMAP\n\r");
            usbSerial.SerialLine.Write("2 Ouvrir verrou\n\r");
            usbSerial.SerialLine.Write("3 Aimant ON\n\r");
            usbSerial.SerialLine.Write("4 Rangement MUPUS\n\r");
            usbSerial.SerialLine.Write("5 Vibrer 10 secondes\n\r");
            usbSerial.SerialLine.Write("6 Aimant OFF\n\r");
            usbSerial.SerialLine.Write("7 Fermer verrou\n\r");
            usbSerial.SerialLine.Write("8 Ranger Romap\n\r");
    
            
            button.ButtonPressed += new Button.ButtonEventHandler(button_ButtonPressed);
            Joystick.Position position; position = new Joystick.Position() ;
            //joystick.JoystickPressed += new Joystick.JoystickEventHandler(joystick_JoystickPressed);
            joystick.JoystickReleased +=new Joystick.JoystickEventHandler(joystick_JoystickReleased);

            UInt32 bitRate = (UInt32)((8 - 1) << 20) | (UInt32)((15 - 1) << 16) | (UInt32)((6 - 1) << 0);    //Channel
            m_busCan = new GHI.Premium.Hardware.CAN(GHI.Premium.Hardware.CAN.Channel.Channel_1, bitRate, 100); //can_dw.Channel.Channel_1
            serie = new GT.Interfaces.Serial(GT.Socket.GetSocket(11, true, null, string.Empty), 200000, GT.Interfaces.Serial.SerialParity.None, GT.Interfaces.Serial.SerialStopBits.One, 8, GT.Interfaces.Serial.HardwareFlowControl.NotRequired, null);
            serie.Open();
            m_busCan.DataReceivedEvent += new GHI.Premium.Hardware.CANDataReceivedEventHandler(can_DataReceivedEvent);
            m_busCan.ErrorReceivedEvent += new GHI.Premium.Hardware.CANErrorReceivedEventHandler(can_ErrorReceivedEvent);
            S11P3dirAx12=extender.SetupDigitalOutput(GT.Socket.Pin.Three, false);
            GT.Timer timer2 = new GT.Timer(2000);
            timer2.Tick += new GT.Timer.TickEventHandler(timer2_Tick);
            timer2.Start();//usb
            multicolorLed.AddGreen();
            

        }
        void joystick_JoystickReleased(Joystick sender, Joystick.JoystickState state)
        {
if (mode == 0)
{
                if (TEST) Debug.Print("Appui Boutton : " + appui.ToString());
                Thread.Sleep(500);//anti-rebond
                switch (appui)
                {
                    case 0: //deployerMUPUS
                        if (TEST) Debug.Print("Déploiement MUPUS");
                        relay_X1.TurnOn();
                        MUPUSDEPLOYE = true;
                        break;
                    case 1: //deployerROMAP
                        if (TEST) Debug.Print("Déploiement ROMAP");
                        int vinit = 5, vfin = 40, pas = 2;
                        vitesseMouv(1, vinit);
                        tourne(1, AngleHautRomap);
                        for (int v = vinit + pas; v < vfin; v = v + pas)
                        {
                            Thread.Sleep(2000 / v);
                            vitesseMouv(1, v);
                        }
                        ETATROMAP = 100;
                        break;
                    case 2: //lacherMarteau
                        if (TEST) Debug.Print("Ouvrir verrou");
                        vitesseMouv(2, 80);
                        tourne(2, 60);
                        LACHE = true;
                        break;
                    case 3: //aimanterMarteau
                        if (TEST) Debug.Print("Aimant ON");
                        S8P7ventouse.Write(true);
                        break;
                    case 4: //rangerMUPUS
                        if (TEST) Debug.Print("Rangement MUPUS");
                        relay_X1.TurnOff();
                        MUPUSDEPLOYE = false;
                        break;
                    case 5: //Vibrer
                        if (TEST) Debug.Print("Vibrer 10 secondes");
                        S8P8pwmMarteau.Set(1000, 0.5);
                        GT.Timer timer = new GT.Timer(10000);
                        timer.Tick += new GT.Timer.TickEventHandler(timer_Tick);
                        timer.Start();
                        break;
                    case 6: //desaimanter
                        if (TEST) Debug.Print("Aimant OFF");
                        S8P7ventouse.Write(false);
                        break;
                    case 7: //verouiller
                        if (TEST) Debug.Print("Fermer verrou");
                        vitesseMouv(2, 80);
                        tourne(2, 240);
                        LACHE = false;
                        break;
                    case 8: //rangerROMAP
                        if (TEST) Debug.Print("Ranger Romap");
                        vinit = 10; vfin = 1; pas = 1;
                        vitesseMouv(1, vinit);
                        tourne(1, AngleBasRomap);
                        for (int v = vinit - pas; v > vfin; v = v - pas)
                        {
                            Thread.Sleep(2000 / v);
                            vitesseMouv(1, v);
                        }
                        ETATROMAP = 0;
                        break;
                }
                appui = (appui + 1) % 9;
}
            /* uint x;
            uint y;
            position = joystick.GetPosition();
            if (position.X < 0) position.X *= -1;
            if (position.Y < 0) position.Y *= -1;
            x = (uint)(position.X * 140);
            y = (uint)(position.Y * 100);
            display.SimpleGraphics.DisplayEllipse(GT.Color.Blue, 160, 120, x, y);*/
        }

        void can_DataReceivedEvent(GHI.Premium.Hardware.CAN sender, GHI.Premium.Hardware.CANDataReceivedEventArgs args)
        {
if(mode==1)
{           m_data[0] = new GHI.Premium.Hardware.CAN.Message();
            m_data[0].IsEID = false;
            m_data[0].IsRTR = false;
            /*
            m_data[0] = new GHI.Premium.Hardware.CAN.Message();
            m_data[0].ArbID = 101;
            m_data[0].Data[0] = 25;
            m_data[0].DLC = 1;
            m_data[0].IsEID = false;
            m_data[0].IsRTR = false;
            m_busCan.PostMessages(m_data, 0, 1);
            */
            if (TEST) Debug.Print(">>> Données CAN Réceptionnées <<<");
            GHI.Premium.Hardware.CAN.Message[] msgList = new GHI.Premium.Hardware.CAN.Message[100];
            for (int i = 0; i < msgList.Length; i++)
                msgList[i] = new GHI.Premium.Hardware.CAN.Message();
            // read as many messages as possible
            int count = sender.GetMessages(msgList, 0, msgList.Length);
            for (int i = 0; i < count; i++)
            {
                if (TEST) Debug.Print("MSG: ID = " + msgList[i].ArbID + " Data : " + ((int)msgList[i].Data[0]).ToString() + ((int)msgList[i].Data[1]).ToString() + " at time = " + msgList[i].TimeStamp);
               /**********************************************************ALLUMER LED*********************************************************************/
               if ((msgList[i].ArbID == 0x7E1 && msgList[i].Data[0] == 1)||(msgList[i].ArbID == 0x6E1 && msgList[i].Data[0] == 1))
               {   if (TEST) Debug.Print("LEDON");
                   Thread.Sleep(2000);
                   ledon();               
               }
               /*********************************************************ETEINDRE LED********************************************************************/
               if ((msgList[i].ArbID == 0x7E1 && msgList[i].Data[0] == 0)||(msgList[i].ArbID == 0x6E1 && msgList[i].Data[0] == 0))
               {   if (TEST) Debug.Print("LEDOFF");
                   Thread.Sleep(2000);
                   ledoff();
               }
               /*******************************************************BOUGER SERVOMOTEUR***************************************************************/
               if ((msgList[i].ArbID == 0x7E0)||(msgList[i].ArbID == 0x6E0))
               {   int data = (msgList[i].Data[0] << 8 )+ msgList[i].Data[1];
                   if (TEST) Debug.Print("Rotation position :" + data.ToString());
                   Thread.Sleep(2000);
                   tourne(1,data);
                   Thread.Sleep(500);
                   tourne(2, data);
               }
               /*******************************************************VITESSE SERVOMOTEUR**************************************************************/
               if ((msgList[i].ArbID == 0x7E3)||(msgList[i].ArbID == 0x6E3))
               {
                   int data = msgList[i].Data[0];
                   if (TEST) Debug.Print("Vitesse Servo :" + data.ToString());
                   Thread.Sleep(2000);
                   vitesseMouv(1,data);
                   Thread.Sleep(500);
                   vitesseMouv(2, data);
               }
               /*******************************************************RESET MUPUS***************************************************************/
               if (msgList[i].ArbID == 0x700)
               {
                   if (TEST) Debug.Print("Reset MUPUS");
                   //ordre reset mupus reçu
                   m_data[0].ArbID = 0x701; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   relay_X1.TurnOff();
                   S8P8pwmMarteau.Set(1000, 0.001);
                   vitesseMouv(2,80);
                   tourne(2,240);

                   S8P7ventouse.Write(false);

                   //reset mupus effectué
                   m_data[0].ArbID = 0x702; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }
               /*******************************************************DEMANDE ETAT MUPUS***************************************************************/
               if (msgList[i].ArbID == 0x710)
               {
                   if (TEST) Debug.Print("Demande état MUPUS");
                   //ordre état mupus reçu
                   m_data[0].ArbID = 0x711; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   //renvoi état mupus                    //bit B0=déployé, B1=vibre, B2=laché
                   ETATMUPUS=0;
                   if (LACHE) ETATMUPUS = ETATMUPUS | 4;
                   if (VIBRE) ETATMUPUS = ETATMUPUS | 2;
                   if (MUPUSDEPLOYE) ETATMUPUS = ETATMUPUS | 1;
                   m_data[0].ArbID = 0x710; m_data[0].Data[0] = (byte)ETATMUPUS; m_data[0].DLC = 1; m_busCan.PostMessages(m_data, 0, 1);
               }
               /*******************************************************DEPLOIEMENT MUPUS***************************************************************/
               if (msgList[i].ArbID == 0x720)
               {
                   if (TEST) Debug.Print("Déploiement MUPUS");        
                   relay_X1.TurnOn();
                   MUPUSDEPLOYE = true;
                   //ordre déployer reçu
                   m_data[0].ArbID = 0x721;   m_data[0].Data[0] = 0; m_data[0].DLC = 0;  m_busCan.PostMessages(m_data, 0, 1);
                   //déploiement terminé
                   Thread.Sleep(5000);
                   m_data[0].ArbID = 0x722; m_data[0].Data[0] = 0; m_data[0].DLC = 0;  m_busCan.PostMessages(m_data, 0, 1);
               }
               /*********************************************************RANGEMENT MUPUS***************************************************************/
               if (msgList[i].ArbID == 0x750)
               {    
                   if (TEST) Debug.Print("Rangement MUPUS");
                   //ordre ranger reçu
                   m_data[0].ArbID = 0x751; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   relay_X1.TurnOff();
                   MUPUSDEPLOYE = false;
                   //rangement terminé
                   Thread.Sleep(5000);
                   m_data[0].ArbID = 0x752; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }
               /***********************************************************ARRET VENTOUSE + Fermer Verrou****************************************************************/
               if (msgList[i].ArbID == 0x760)
               {
                   if (TEST) Debug.Print("Fermer verrou");
                   //ordre réactiver verrou désactiver maintien au sol reçu
                   m_data[0].ArbID = 0x761; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   vitesseMouv(2,80);
                   tourne(2,240);

                    S8P7ventouse.Write(false);
                    LACHE = false;
                    //verrou réactivé, maintien au sol désactivé
                    m_data[0].ArbID = 0x762; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }
               /************************************************************ACTION VENTOUSE + Ouvrir Verrou*******************************************************************/
               if (msgList[i].ArbID == 0x740)
               {
                   if (TEST) Debug.Print("Ouvrir verrou");
                   //ordre ouvrir verrou activer maintien au sol reçu (lâcher marteau)
                   m_data[0].ArbID = 0x741; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   vitesseMouv(2,80);
                   tourne(2,60);
                   
                   S8P7ventouse.Write(true);
                   LACHE = true;
                   //verrou ouvert   maintien au sol activé (marteau lâché)
                   m_data[0].ArbID = 0x742; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }
               /***********************************************************ARRET AIMANT****************************************************************/
               if (msgList[i].ArbID == 0x7E7 && msgList[i].Data[0] == 0)
               {
                   if (TEST) Debug.Print("Aimant OFF");
                     S8P7ventouse.Write(false);
               }
               /************************************************************AIMANTER*******************************************************************/
               if (msgList[i].ArbID == 0x7E7 && msgList[i].Data[0] == 1)
               {
                   if (TEST) Debug.Print("Aimant ON");
                     S8P7ventouse.Write(true);
               }
               /*************************************************************VIBRER Xsecondes*********************************************************************/
               if (msgList[i].ArbID == 0x730)
               {   //ordre vibrer X secondes
                   m_data[0].ArbID = 0x731; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   byte secondes = msgList[i].Data[0];
                   if (TEST) Debug.Print("Vibrer "+secondes.ToString()+" secondes");
                   S8P8pwmMarteau.Set(1000, 0.5);   
                   GT.Timer timer = new GT.Timer(secondes * 1000);
                   timer.Tick += new GT.Timer.TickEventHandler(timer_Tick);
                   timer.Start();
               }
               /**********************************************************VITESSE VIBREUR***************************************************************/
               if (msgList[i].ArbID == 0x7E6)
               {
                   float VitessePourcent = (float)msgList[i].Data[0] / 100;
                   if (TEST) Debug.Print("Vitesse marteau " + VitessePourcent.ToString() + " %");
                   S8P8pwmMarteau.Set(1000, VitessePourcent);
               }
                /*************************************************************VIBRER mode test*********************************************************************/
               if (msgList[i].ArbID == 0x7E5)
               {
                   if (TEST) Debug.Print("Vibrer");
                   if (msgList[i].Data[0] == 0)
                           S8P8pwmMarteau.Set(1000, 0.001);
                   else
                           S8P8pwmMarteau.Set(1000, 0.5);
                   
               }
               /*******************************************************RESET ROMAP***************************************************************/
               if (msgList[i].ArbID == 0x600)
               {   if (TEST) Debug.Print("Reset Romap");
                   int vinit = 10;
                   //ordre reset romap reçu
                   m_data[0].ArbID = 0x601; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   vitesseMouv(1,vinit);
                   tourne(1,AngleBasRomap); 
                   //romap réinitialisé
                   m_data[0].ArbID = 0x602; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }
               /*******************************************************ETAT ROMAP***************************************************************/
               if (msgList[i].ArbID == 0x610)
               {
                   if (TEST) Debug.Print("Etat Romap");
                   //demande état romap reçu
                   m_data[0].ArbID = 0x611; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   //envoi état romap
                   m_data[0].ArbID = 0x610; m_data[0].Data[0] = (byte)ETATROMAP; m_data[0].DLC = 1; m_busCan.PostMessages(m_data, 0, 1);

               }
               /*******************************************************DEPLOYER ROMAP***************************************************************/
               if (msgList[i].ArbID == 0x630)
               {
                   if (TEST) Debug.Print("Déploiement ROMAP");
                   int vinit =5, vfin=40, pas=2;
                   //ordre déployer romap reçu
                   m_data[0].ArbID = 0x631; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   vitesseMouv(1,vinit);
                   tourne(1,AngleHautRomap);
                   for (int v = vinit + pas; v < vfin; v = v + pas)
                       {
                           Thread.Sleep(2000 / v);
                           vitesseMouv(1,v);
                       }
                   ETATROMAP = 100;
                   //déploiement romap terminé
                   m_data[0].ArbID = 0x632; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }
               /*******************************************************RANGER ROMAP***************************************************************/
               if (msgList[i].ArbID == 0x620)
               {
                   if (TEST) Debug.Print("Ranger Romap");
                   int vinit = 10, vfin = 1, pas = 1;
                   //ordre ranger romap reçu
                   m_data[0].ArbID = 0x621; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
                   vitesseMouv(1,vinit);
                   tourne(1,AngleBasRomap); 
                   for (int v = vinit - pas; v > vfin; v = v - pas)
                   {
                           Thread.Sleep(2000 / v);
                           vitesseMouv(1,v);
                   }
                   ETATROMAP = 0;
                   //rangement romap terminé
                   m_data[0].ArbID = 0x622; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
               }

            }
}
        }

        void can_ErrorReceivedEvent(GHI.Premium.Hardware.CAN sender, GHI.Premium.Hardware.CANErrorReceivedEventArgs args)
        {
            if (TEST) Debug.Print(">>> Erreur bus CAN <<<");
        }

        void timer_Tick(GT.Timer timer)
        {
            m_data[0] = new GHI.Premium.Hardware.CAN.Message();
            m_data[0].IsEID = false;
            m_data[0].IsRTR = false;
            S8P8pwmMarteau.Set(1000, 0.001);
            timer.Stop();
            //vibrer X secondes terminer
            m_data[0].ArbID = 0x732; m_data[0].Data[0] = 0; m_data[0].DLC = 0; m_busCan.PostMessages(m_data, 0, 1);
        }

        void timer2_Tick(GT.Timer timer2)
        {
            if (mode == 2)
            {
                action = usbSerial.SerialLine.ReadByte();//blocant
                //usbSerial.SerialLine.WriteLine(action.ToString());
                switch (action)
                {
                    case '0': //deployerMUPUS
                        if (TEST) Debug.Print("Déploiement MUPUS");
                        relay_X1.TurnOn();
                        MUPUSDEPLOYE = true;
                        break;
                    case '1': //deployerROMAP
                        if (TEST) Debug.Print("Déploiement ROMAP");
                        int vinit = 5, vfin = 40, pas = 2;
                        vitesseMouv(1, vinit);
                        tourne(1, AngleHautRomap);
                        for (int v = vinit + pas; v < vfin; v = v + pas)
                        {
                            Thread.Sleep(2000 / v);
                            vitesseMouv(1, v);
                        }
                        ETATROMAP = 100;
                        break;
                    case '2': //lacherMarteau
                        if (TEST) Debug.Print("Ouvrir verrou");
                        vitesseMouv(2, 80);
                        tourne(2, 60);
                        LACHE = true;
                        break;
                    case '3': //aimanterMarteau
                        if (TEST) Debug.Print("Aimant ON");
                        S8P7ventouse.Write(true);
                        break;
                    case '4': //rangerMUPUS
                        if (TEST) Debug.Print("Rangement MUPUS");
                        relay_X1.TurnOff();
                        MUPUSDEPLOYE = false;
                        break;
                    case '5': //Vibrer
                        if (TEST) Debug.Print("Vibrer 10 secondes");
                        S8P8pwmMarteau.Set(1000, 0.5);
                        Thread.Sleep(10000);
                        S8P8pwmMarteau.Set(1000, 0.001);
                        //GT.Timer timer = new GT.Timer(10000);
                        //timer.Tick += new GT.Timer.TickEventHandler(timer_Tick);
                        //timer.Start();
                        break;
                    case '6': //desaimanter
                        if (TEST) Debug.Print("Aimant OFF");
                        S8P7ventouse.Write(false);
                        break;
                    case '7': //verouiller
                        if (TEST) Debug.Print("Fermer verrou");
                        vitesseMouv(2, 80);
                        tourne(2, 240);
                        LACHE = false;
                        break;
                    case '8': //rangerROMAP
                        if (TEST) Debug.Print("Ranger Romap");
                        vinit = 10; vfin = 1; pas = 1;
                        vitesseMouv(1, vinit);
                        tourne(1, AngleBasRomap);
                        for (int v = vinit - pas; v > vfin; v = v - pas)
                        {
                            Thread.Sleep(2000 / v);
                            vitesseMouv(1, v);
                        }
                        ETATROMAP = 0;
                        break;
                }
}
            
        }

       void button_ButtonPressed(Button sender, Button.ButtonState state)
        {
            if (mode == 0) { multicolorLed.AddBlue(); multicolorLed.RemoveGreen(); multicolorLed.RemoveRed(); }
            if (mode == 1) { multicolorLed.RemoveBlue(); multicolorLed.RemoveGreen(); multicolorLed.AddRed(); }
            if (mode == 2) { multicolorLed.RemoveBlue(); multicolorLed.AddGreen(); multicolorLed.RemoveRed(); }
            mode=mode+1;
        }

        void zero()
        {
            S11P3dirAx12.Write(true);
            byte[] TabZero = new byte[] { 0xFF, 0xFF, 0X01, 0x05, 0x03, 0x1E, 0x00, 0x02, 0 };
            int CS = 0;
            int NbOctzero = 9;

            for (int i = 2; i < NbOctzero - 1; i++)
            {
                CS += TabZero[i];
            }

            TabZero[8] = (byte)(~CS & 0xff);
            serie.Write(TabZero);
            S11P3dirAx12.Write(false);
            Thread.Sleep(2);

        }

        void ledon()
        {
            byte[] TabLedon = new byte[] { 0xFF, 0xFF, 0x01, 0x04, 0x03, 0x19, 0x01, 0 };//0xDD
            int CS = 0;
            int NbOctledon = 8;
            S11P3dirAx12.Write(true);
            for (int i = 2; i < NbOctledon - 1; i++)
            {
                CS += TabLedon[i];

            }

            TabLedon[7] = (byte)(~CS & 0xff);
            //Debug.Print("valeur CS");
            //Debug.Print(TabLedon[7].ToString());
            serie.Write(TabLedon);
            S11P3dirAx12.Write(false);
            Thread.Sleep(2);

        }

        void ledoff()
        {
            byte[] TabLedoff = new byte[] { 0xFF, 0xFF, 0x01, 0x04, 0x03, 0x19, 0x00, 0 }; //0xDE
            int CS = 0;
            int NbOctledoff = 8;
            S11P3dirAx12.Write(true);
            for (int i = 2; i < NbOctledoff - 1; i++)
            {
                CS += TabLedoff[i];

            }

            TabLedoff[7] = (byte)(~CS & 0xff);
            //Debug.Print("valeur CS");
            //Debug.Print(TabLedoff[7].ToString());
            serie.Write(TabLedoff);
            S11P3dirAx12.Write(false);
            Thread.Sleep(2);

        }

        void tourne(int id,int degre)
        {   
            int valhex = degre * 0x3ff / 300;
            S11P3dirAx12.Write(true);
            byte[] TabTourne = new byte[] { 0xFF, 0xFF, 0X01, 0x05, 0x03, 0x1E, 0, 0, 0 };
            TabTourne[2] = (byte)id;
            TabTourne[6] = (byte)(valhex & 0xff);
            TabTourne[7] = (byte)(valhex >> 8);

            int CS = 0;
            int NbOctTrameTourne = 9;

            for (int i = 2; i < NbOctTrameTourne - 1; i++)
            {
                CS += TabTourne[i];
            }

            TabTourne[8] = (byte)(~CS & 0xff);
            //Debug.Print("Angle");
            //Debug.Print(TabTourne[6].ToString());
            //Debug.Print(TabTourne[7].ToString());
            //Debug.Print(TabTourne[8].ToString());
            serie.Write(TabTourne);
            S11P3dirAx12.Write(false);
            Thread.Sleep(2);
        }

        void generPwm()
        {
            S8P8pwmMarteau = extender2.SetupPWMOutput(GT.Socket.Pin.Eight);           
            S8P8pwmMarteau.Set(1000, 0.5);                    
        }



        void vitesseMouv(int id, int speed)//0-114)
        {
            int CS = 0;
            int NbOctTrametVitesse = 9;
            int valeurVitesseHex = speed * 0x3ff / 114;
            S11P3dirAx12.Write(true);
            byte[] TabVitesse = new byte[] { 0xFF, 0xFF, 0x01, 0x05, 0x03, 0x20, 0, 0, 0 };
            TabVitesse[2] = (byte)id;
            TabVitesse[6] = (byte)(valeurVitesseHex & 0xFF);
            TabVitesse[7] = (byte)(valeurVitesseHex >> 8);
           

            for (int i = 2; i < NbOctTrametVitesse - 1; i++)
            {
                CS += TabVitesse[i];
            }

            TabVitesse[8] = (byte)(~CS & 0xff);
            //Debug.Print("CS vitesse + vitesse : ");
            //Debug.Print(TabVitesse[6].ToString());
            //Debug.Print(TabVitesse[7].ToString());
            //Debug.Print(TabVitesse[8].ToString());
            serie.Write(TabVitesse);
            S11P3dirAx12.Write(false);
            Thread.Sleep(2);

        }
    }
}
