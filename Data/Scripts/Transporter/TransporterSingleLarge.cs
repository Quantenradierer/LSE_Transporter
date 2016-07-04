using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Collections;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRageMath;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSE
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), new string[] { "TransporterSingleLarge" })]
    class TransporterSingleLarge : LSE.Teleporter.Teleporter
    {
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			TeleporterDiameter = 1.25; // meters

			TeleporterCenter = new Vector3(0.0f, -0.9f, 0.0f);
			TeleporterPads = new List<Vector3>() {
				new Vector3(0.0f, 0.0f, 0.0f)
			};
			Subtype = "TransporterSingleLarge";


            if (!s_Configs.ContainsKey(Subtype))
            {
			    var message = new LSE.TransporterNetwork.MessageConfig ();
			    message.Side = LSE.TransporterNetwork.MessageSide.ClientSide;
			    message.GPSTargetRange = 5.0; // meters
			    message.MaximumRange = 30 * 1000; // meters
			    message.PowerPerKilometer = 1;  // megawatt
			    message.ValidTypes = new List<int>() {0, 1, 2, 3, 4, 5, 6};
			    message.Subtype = Subtype;

			    SetConfig (Subtype, message);
            }

			base.Init(objectBuilder);
        }
    }
}
