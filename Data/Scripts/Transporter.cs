using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Collections;
using ProtoBuf;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using Ingame = VRage.Game.ModAPI.Ingame;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRageMath;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;


namespace LSE
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), new string[] { "Transporter" })]
    class Transporter : LSE.Teleporter.Teleporter
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			TeleporterDiameter = 2.5; // meters
			TeleporterCenter = new Vector3(0.0f, -0.9f, 0.0f);

			TeleporterPads = new List<Vector3>() {
				new Vector3(-1.4f,  -0.9f, 0.8f),
				new Vector3(-1.4f, -0.9f, -0.8f),
				new Vector3(1.4f, -0.9f, 0.8f),
				new Vector3(1.4f, -0.9f, -0.8f),
				new Vector3(0f, -0.9f, 1.4f),
				new Vector3(0f, -0.9f, -1.4f)
			};

			Subtype = "Transporter";

            if (!s_Configs.ContainsKey(Subtype))
            {
			    var message = new LSE.TransporterNetwork.MessageConfig ();
			    message.Side = LSE.TransporterNetwork.MessageSide.ClientSide;
			    message.GPSTargetRange = 5.0; // meters
			    message.MaximumRange = 60 * 1000; // meters
			    message.PowerPerKilometer = 1;  // megawatt
			    message.ValidTypes = new List<int>() {0, 1, 2, 3, 4, 5, 6};
			    message.Subtype = Subtype;

			    SetConfig (Subtype, message);
            }

            base.Init(objectBuilder);
        }
    }
}
