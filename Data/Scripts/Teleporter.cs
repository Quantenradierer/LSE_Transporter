/*
Copyright © 2016 Leto
This work is free. You can redistribute it and/or modify it under the
terms of the Do What The Fuck You Want To Public License, Version 2,
as published by Sam Hocevar. See http://www.wtfpl.net/ for more details.
*/


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


namespace LSE.Teleporter
{
    public enum TeleportError
    {
        None,
        MissingPower,
        NoTarget,
        NoPlayer,
        TargetBlocked,
        GPSOutOfRange
    }

    public class TeleportInformation
    {
        public IMyEntity Entity;
        public TransportEndpoint Endpoint;
        public IMyEntity Transporter;
        public MyParticleEffect StartEffect;
        public MyParticleEffect EndEffect;

        public void AddSound()
        {
            {
                var emitter = new Sandbox.Game.Entities.MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)Entity);
                var pair = new Sandbox.Game.Entities.MySoundPair("Transporter");
                emitter.PlaySound(pair);
            }
        }

        public MatrixD? GetTargetMatrix(bool recalc=false)
        {
            var matrix = new MatrixD(Entity.WorldMatrix);
            var translation = Endpoint.GetCurrentPosition(recalc);
            if (translation == null)
            {
                // no free space
                MyAPIGateway.Utilities.ShowNotification("No free space");
                return null;
            }
            if (Endpoint.Entity != null)
            {
                matrix = Endpoint.Entity.WorldMatrix;
                //var forward = Endpoint.Entity.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
            }
            matrix.Translation = translation.Value;
            return matrix;
        }

        public void UpdateEffects(bool recalc=false)
        {
            if (StartEffect == null &&
                MyParticlesManager.TryCreateParticleEffect(53, out StartEffect, false))
            {
                AddSound();
            }

            if (EndEffect == null &&
                MyParticlesManager.TryCreateParticleEffect(53, out EndEffect, false))
            {
            }

            var rotMatrix = VRageMath.MatrixD.CreateRotationX(-Math.PI / 2);
            if (StartEffect != null)
            {
                var matrixEntity = new MatrixD(Entity.WorldMatrix);
                matrixEntity.Translation = matrixEntity.Translation + matrixEntity.Down * 4.5;
                matrixEntity = rotMatrix * matrixEntity;
                
                StartEffect.WorldMatrix = matrixEntity;
                StartEffect.UserScale = 0.07f;
                StartEffect.UserBirthMultiplier = 1.0f;
                StartEffect.UserAxisScale = new Vector3(1.1, 1.1, 1.1);
            }

            if (EndEffect != null)
            {
                var matrix = GetTargetMatrix(recalc);
                if (matrix != null)
                {
                    var matrixEntity = matrix.Value;
                    matrixEntity.Translation = matrixEntity.Translation + matrixEntity.Down * 4.5;
                    matrixEntity = rotMatrix * matrixEntity;
                    EndEffect.WorldMatrix = matrixEntity;
                    EndEffect.UserScale = 0.07f;
                    EndEffect.UserBirthMultiplier = 1.0f;
                    EndEffect.UserAxisScale = new Vector3(1.1, 1.1, 1.1);
                }
            }
        }

        public void StopEffects()
        {

        }

        public double Distance
        {
            get
            {
                if (Entity != null && Endpoint != null)
                {
                    var pos = Endpoint.GetCurrentPosition();
                    if (pos != null)
                    {
                        return (pos.Value - Entity.GetPosition()).Length();
                    }
                }
                return 0.0f;
            }
        }
    }


    public class TransportEndpoint
    {
        public IMyEntity Entity;
        public Vector3D Position = Vector3D.Zero;
        public bool RecalculateWhenBlocked = true;

        public Vector3D? GetCurrentPosition(bool recalculate=true)
        {
            var position = Position;
            if (Entity != null)
            {
                position += Entity.GetPosition();
            }

            if (recalculate && RecalculateWhenBlocked)
            {
                var realPos = MyAPIGateway.Entities.FindFreePlace(position, 0.8f, 21, 7, 0.2f);
                if (realPos == null)
                {
                    realPos = MyAPIGateway.Entities.FindFreePlace(position, 0.8f, 21, 7, 0.8f);
                    if (realPos == null)
                    {
                        realPos = MyAPIGateway.Entities.FindFreePlace(position, 0.8f, 21, 7, 3.2f);
                    }
                }

                if (realPos == null)
                {
                    return null;
                }
                else
                {
                    if (Entity != null)
                    {
                        Position = realPos.Value - Entity.GetPosition();
                        position = realPos.Value;
                    }
                }
            }
            return position;
        }
    }


    public class Teleporter : GameLogicComponent
    {
        public static HashSet<Teleporter> TeleporterList = new HashSet<Teleporter>();

        public double GPSTargetRange
		{
			get
			{
				return GetConfig().GPSTargetRange; 
			}	
		}

		public double MaximumRange
		{
			get
			{
				return GetConfig().MaximumRange;
			}	
		}

		public float PowerPerKilometer
		{
			get
			{
				return GetConfig().PowerPerKilometer;
			}	
		}

		public double MAX_PLANET_SIZE
		{
			get
			{
				return GetConfig().PlanetRange;
			}
		}

		public List<int> ValidTypes
		{
			get
			{
				return GetConfig ().ValidTypes;
			}
		}

        public int ProfilesAmount
        {
            get
            {
                return 2;
            }
        }

        public bool BeamEnemies
        {
            get
            {
                return GetConfig().BeamEnemy;
            }
        }



        public string Subtype = "Transporter";
        public double TeleporterDiameter = 2.5;

        public Vector3D TeleporterCenter = new Vector3(0.0f, 0.0f, -0.9f);
		public List<Vector3> TeleporterPads = new List<Vector3>() { };

        public int TELEPORT_TIME = 10; // effect timespan is 10 seconds... but time in game runs faster (?!)


        public IMyCubeBlock CubeBlock;
        public bool FirstStart = true;
        public static bool FirstStartStatic = true;
        public double Distance = 1;

        public LSE.Control.ButtonControl<Sandbox.ModAPI.Ingame.IMyOreDetector> Button;
        public LSE.Control.ControlAction<Sandbox.ModAPI.Ingame.IMyOreDetector> ActionBeam;

        public ProfileListbox<Sandbox.ModAPI.Ingame.IMyOreDetector> ProfileListbox;
        public Control.SwitchControl<Sandbox.ModAPI.Ingame.IMyOreDetector> SwitchControl;
        public TargetCombobox<Sandbox.ModAPI.Ingame.IMyOreDetector> TargetsListbox;
        public ProfileTextbox<Sandbox.ModAPI.Ingame.IMyOreDetector> ProfileTextbox;

        public MyDefinitionId PowerDefinitionId = new VRage.Game.MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity");
        public Sandbox.Game.EntityComponents.MyResourceSinkComponent Sink;
        
        public Dictionary<int, Network.MessageProfile> Profiles = new Dictionary<int, Network.MessageProfile>();

        public List<TeleportInformation> TeleportingInfos = new List<TeleportInformation>();
        public DateTime? TeleportStart = null;


        public static Dictionary<string, Network.MessageConfig> s_Configs = new Dictionary<string, Network.MessageConfig>();
        public static void SetConfig(string subtype, Network.MessageConfig config)
		{
			s_Configs[subtype] = config;
		}

		public LSE.Network.MessageConfig GetConfig()
		{
			if (s_Configs.ContainsKey(Subtype))
			{
				return s_Configs [Subtype];
			}
			return new LSE.Network.MessageConfig ();
		}

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            CubeBlock = (IMyCubeBlock)Entity;
            if (!(CubeBlock).BlockDefinition.SubtypeName.Contains(Subtype)) { return; }

            CubeBlock.Components.TryGet<Sandbox.Game.EntityComponents.MyResourceSinkComponent>(out Sink);
            Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);
            CubeBlock.IsWorkingChanged += IsWorkingChanged;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

			((IMyFunctionalBlock)Entity).AppendingCustomInfo += AppendingCustomInfo;

		}

        float CalcRequiredPower()
        {
			return Math.Max((float)Distance / 1000 * PowerPerKilometer, 0.001f);
        }

        bool IsEnoughPower(double distance)
        {
			return Sink.IsPowerAvailable(PowerDefinitionId, (float)distance / 1000 * PowerPerKilometer);
        }

        void IsWorkingChanged(IMyCubeBlock block)
        {
            if (!FirstStart)
            {
                try
                {
                    var teleporter = block.GameLogic.GetAs<Teleporter>();
                    if (block.IsWorking)
                    {
                        Teleporter.TeleporterList.Add(teleporter);
                    }
                    else
                    {
                        Teleporter.TeleporterList.Remove(teleporter);
                        TeleportingInfos.Clear();
                        TeleportStart = null;
                        Distance = 0;
                    }
                    teleporter.DrawEmissive();
                }
                catch
                {
                }
            }
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
			if (!(CubeBlock).BlockDefinition.SubtypeName.Contains(Subtype)) { return; }

            if (FirstStartStatic)
            {
                FirstStartStatic = false;
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    foreach (var subtype in new List<string>(s_Configs.Keys))
                    {
                        //byte[] byteData = System.Text.Encoding.UTF8.GetBytes(xml);
                        if (!MyAPIGateway.Utilities.FileExistsInGlobalStorage(subtype + ".xml"))
                        {
                            var message = new LSE.Network.MessageConfig();
                            var xml = MyAPIGateway.Utilities.SerializeToXML<Network.MessageConfig>(s_Configs[subtype]);
                            var writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(subtype + ".xml");
                            writer.Write(xml);
                            writer.Close();
                        }
                        var reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(subtype + ".xml");
                        var text = reader.ReadToEnd();
                        var messageNew = MyAPIGateway.Utilities.SerializeFromXML<Network.MessageConfig>(text);
                        SetConfig(subtype, messageNew);
                    }
                }
                else
                {
                    LSE.Network.MessageUtils.SendMessageToServer(new Network.MessageClientConnected());
                }
            }

            if (FirstStart)
            {

                CreateUI();
                UpdateVisual();
                LoadProfiles();

                FirstStart = false;
            }

            if (TeleportStart == null)
            {
                return;
            }

            var transportTime = TeleportStart + new TimeSpan(0, 0, TELEPORT_TIME);

            
            foreach (var info in TeleportingInfos)
            {
                info.UpdateEffects();
            }

            if (MyAPIGateway.Session.GameDateTime > transportTime)
            {
                // TODO: Add distance  / power check
                double distance = 0.0f;
                foreach (var info in TeleportingInfos)
                {
                    var matrix = info.GetTargetMatrix(true);

                    if (matrix != null && IsPositionInRange(matrix.Value.Translation))
                    {
                        if (IsEnoughPower(distance + info.Distance))
                        {
                            distance += info.Distance;
                            var matrixEntity = matrix.Value;
                            matrixEntity.Translation = matrixEntity.Translation + matrixEntity.Down * 0.9;
                            info.Entity.SetWorldMatrix(matrixEntity);
                        }
                    }
                    info.StopEffects();
                }


                TeleportingInfos.Clear();
                TeleportStart = null;
                Distance = 0;
                Sink.Update();
            }

                /*
                var maxLength = 0.003;
                var antiVelocity = (info.StartEffect.Value - entity.GetPosition());
                
                if (antiVelocity.Length() > maxLength)
                {
                    antiVelocity.Normalize();
                    entity.SetPosition(info.Start.Value - antiVelocity * maxLength);
                    info.Start = entity.GetPosition();
                }
                //entity.Physics.Clear();
                    
                if (diff > 1)
                {
                    TeleportFromTo(info.Player, info.Target.Value);
                }
                 * */

            //}
        }


        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            if (!(CubeBlock).BlockDefinition.SubtypeName.Contains(Subtype)) { return; }


            DrawEmissive();

            if (CubeBlock.IsWorking)
            {
                TeleporterList.Add(this);
            }
            else
            {
                TeleporterList.Remove(this);
            }
        }

        public void DrawEmissive()
        {
            return;
            /*
            if (!CubeBlock.IsWorking)
            {
                CubeBlock.SetEmissiveParts("Emissive", new Color(255, 0, 0), 0.0f);
                return;
            }
            bool validTeleport = false;
            foreach (var teleportInfo in BeamUpPossible())
            {
                if (teleportInfo.Player != null)
                {
                    validTeleport = true;
                }
            }

            if (validTeleport)
            {
                CubeBlock.SetEmissiveParts("Emissive", new Color(0, 128, 0), 1.0f);
                return;
            }
            else
            {
                CubeBlock.SetEmissiveParts("Emissive", new Color(32, 16, 0), 0.0f);
                return;
            }*/
        }

        /*
                if (teleportInfo.Error == TeleportError.None)
                {
                    CubeBlock.SetEmissiveParts("Emissive", new Color(0, 128, 0), 1.0f);
                    continue;
                }   
                else if (teleportInfo.Error == TeleportError.MissingPower)
                {
                    CubeBlock.SetEmissiveParts("Emissive", new Color(255, 0, 0), 0.0f);
                    //details.Append("WARNING: Not enough power. \r\n");
                    break;
                }
                else if (teleportNr == 0)
                {
                    CubeBlock.SetEmissiveParts("Emissive", new Color(32, 16, 0), 0.0f);
                    /*
                    if (teleportInfo.Error == TeleportError.NoPlayer)
                    {
                    //    details.Append("WARNING: No life signs at the given GPS/no charactere selected. \r\n");
                    }
                    if (teleportInfo.Error == TeleportError.NoTarget)
                    {
                        details.Append("WARNING: No Target selected. \r\n");
                    }
                    if (teleportInfo.Error == TeleportError.TargetBlocked)
                    {
                        details.Append("WARNING: Selected Target may be blocked. \r\n");
                    }
                    if (teleportInfo.Error == TeleportError.GPSOutOfRange)
                    {
                        details.Append("WARNING: Target out of Range. \r\n");
                    }*/
            //details.Append("Teleportation Power Consumption: " + (Distance / 1000).ToString() + "MW");
            //details.Append(lifeSigns.ToString() + " Life signs to teleport.");

        public void BeamUpAll(int profileNr)
        {
            var infos = BeamUpPossible(profileNr);

            Distance = CalcNeededPower(infos);
            TeleportingInfos = infos;
            TeleportStart = MyAPIGateway.Session.GameDateTime;

            foreach (var info in infos)
            {
                info.UpdateEffects(true);
            }

            Sink.Update();
        }


        public override void MarkForClose()
        {
            try
            {
                Teleporter.TeleporterList.Remove(this);
            }
            catch
            {
            }
        }

        public void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            /*
            stringBuilder.Clear();
            stringBuilder.Append("Saved Profile \r\n");
            stringBuilder.Append("Teleport: \r\n");

            foreach (var player in GetTeleportingPlayers(GetSavedType()))
            {
                if (player != null)
                {
                    stringBuilder.Append(player.DisplayName + " \r\n");
                }
            }

            var target = "No Target";
            switch (GetSavedType())
            {
                case 0:
                    target = GetSavedGPSName();
                    break;
                case 1:
                    var playersTo = GetSavedPlayers();
                    if (playersTo.Count > 0)
                    {
                        target = playersTo[0].DisplayName;
                    }
                    break;
                case 2:
                case 3:
                case 6:
                    target = "Teleporter";
                    break;
                case 5:
                    target = "Foreign Teleporter";
                    break;
                case 4:
                    var targetPos = GetPlanetPosInRange();
                    if (targetPos == null)
                    {
                        target = "No Planet in range";
                    }
                    else
                    {
                        target = "Planet in range";
                    }
                    break;
                default:
                    break;
            }
            
            stringBuilder.Append("To: " + target + " \r\n");

            */
        }

        public List<IMyEntity> GetTeleportingEntities(Network.MessageProfile profile)
        {
            var entities = new List<IMyEntity>();
            if (!profile.From)
            {
                var players = GetPlayersByPosition(GetTeleporterCenter(), TeleporterDiameter * TeleporterDiameter);
                entities = players.ConvertAll<IMyEntity>((x) => x.Controller.ControlledEntity.Entity);
            }
            else
            {
                foreach (var target in profile.Targets)
                {
                    //try
                    //{
                        switch (target.Type)
                        {
                            case 0:
                                if (target.TargetPos != null)
                                {
                                    var players = GetPlayersByPosition(target.TargetPos.Value, GPSTargetRange * GPSTargetRange);
                                    entities.AddList(players.ConvertAll<IMyEntity>((x) => x.Controller.ControlledEntity.Entity));
                                }
                                break;
                            case (1):
                                var entity = target.GetPlayerEntity();
                                if (entity != null)
                                {
                                    entities.Add(entity);
                                }
                                break;
                            case (2):
                                var foreignTransporter = target.GetTarget();
                                var teleporter = foreignTransporter.GameLogic.GetAs<Teleporter>();

                                var players2 = teleporter.GetPlayersByPosition(
                                    foreignTransporter.GetPosition(),
                                    teleporter.TeleporterDiameter * teleporter.TeleporterDiameter);
                                entities.AddList(players2.ConvertAll<IMyEntity>((x) => x.Controller.ControlledEntity.Entity));
                                break;
                            case (3):
                                // shouldn't exist
                                break;
                        }
                    ///}
                    //catch
                    //{
                    //}
                }
            }
            return entities;
        }

        public List<TransportEndpoint> GetEndpoints(Network.MessageProfile profile)
        {
            
            var targets = new List<TransportEndpoint>();
            if (profile.From)
            {
                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                {
                    var relativePos = GetRelativePosition(Entity.GetPosition(), teleportNr);
                    targets.Add(new TransportEndpoint() { Position = relativePos, Entity = Entity, RecalculateWhenBlocked = false });
                }
            }
            else
            {
                foreach (var target in profile.Targets)
                {
                    switch (target.Type)
                    {
                        case 0:
                            if (target.TargetPos != null)
                            {
                                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                                {
                                    var relativePos = GetRelativePosition(Entity.GetPosition(), teleportNr);
                                    targets.Add(new TransportEndpoint() { Position = target.TargetPos.Value + relativePos });
                                }
                            }
                            break;
                        case 1:
                            var entity = target.GetPlayerEntity();
                            if (entity != null)
                            {
                                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                                {
                                    var relativePos = GetRelativePosition(Entity.GetPosition(), teleportNr);
                                    targets.Add(new TransportEndpoint() { Entity = entity, Position = relativePos });
                                }
                            }
                            break;
                        case 2:
                            var entity2 = target.GetTarget();
                            if (entity2 != null)
                            {
                                var teleporter = entity2.GameLogic.GetAs<Teleporter>();
                                for (int teleportNr = 0; teleportNr < teleporter.TeleporterPads.Count; ++teleportNr)
                                {
                                    var relativePos = teleporter.GetRelativePosition(entity2.GetPosition(), teleportNr);
                                    targets.Add(new TransportEndpoint() { Entity = entity2, Position = relativePos, RecalculateWhenBlocked=false });
                                }
                            }
                            break;
                        case 3:
                            var entity3 = target.GetTarget();
                            if (entity3 != null)
                            {
                                var position = Entity.GetPosition();
                                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                                {
                                    var planet = (Sandbox.Game.Entities.MyPlanet)entity3;
                                    var surfacePosition = planet.GetClosestSurfacePointGlobal(ref position);
                                    targets.Add(new TransportEndpoint() { Position = surfacePosition });
                                }
                            }
                            break;
                    }
                }
            }
            return targets;
        }

        public double CalcNeededPower(List<TeleportInformation> infos)
        {
            double distance = 0;
            foreach (var info in infos)
            {
                distance += info.Distance;
            }
            return distance;
        }

        public List<TeleportInformation> BeamUpPossible(int profileNr)
        {
            var profile = GetProfile(profileNr);
            var teleportInfos = new List<TeleportInformation>();

            var teleportEntities = GetTeleportingEntities(profile);
            teleportEntities.RemoveAll((x) => !IsPositionInRange(x.GetPosition()));
            var endpoints = GetEndpoints(profile);
            endpoints.RemoveAll((x) => !IsPositionInRange(x.GetCurrentPosition()));
            var maxTransports = Math.Min(Math.Min(teleportEntities.Count, endpoints.Count), TeleporterPads.Count);

            double distance = 0;
            MyAPIGateway.Utilities.ShowNotification("ZA" + teleportEntities.Count.ToString() + "|" + endpoints.Count.ToString(), 6000);
            for (var teleportNr = 0; teleportNr < maxTransports; ++teleportNr)
            {   
                var info = new TeleportInformation() { Entity = teleportEntities[teleportNr], Endpoint = endpoints[teleportNr], Transporter = Entity };
                if (IsEnoughPower(info.Distance + distance))
                {
                    distance += info.Distance;
                    teleportInfos.Add(info);
                }
                MyAPIGateway.Utilities.ShowNotification("ZB" + info.Distance.ToString(), 6000);
            }
            return teleportInfos;
        }

        /*
        public Vector3D GetRelativePosition(Vector3 target, int teleportNr)
        {
            return target + (Entity.WorldMatrix.Forward + Entity.WorldMatrix.Right + Entity.WorldMatrix.Up) * TeleporterPads[teleportNr];
        }

        public Vector3D GetTeleporterCenter()
        {
            return Entity.GetPosition() + (Entity.WorldMatrix.Forward + Entity.WorldMatrix.Right + Entity.WorldMatrix.Up) * TeleporterCenter;
        }
        */

        public Vector3D GetRelativePosition(Vector3 target, int teleportNr)
        {
            return (Entity.WorldMatrix.Forward * TeleporterPads[teleportNr].X + Entity.WorldMatrix.Right * TeleporterPads[teleportNr].Z + Entity.WorldMatrix.Up * TeleporterPads[teleportNr].Y);
        }

        public Vector3D GetTeleporterCenter()
        {
            return Entity.GetPosition() + (Entity.WorldMatrix.Forward * TeleporterCenter.X + Entity.WorldMatrix.Right * TeleporterCenter.Z + Entity.WorldMatrix.Up * TeleporterCenter.Y);
        }

        public MatrixD VectorToMatrix(Vector3D target)
        {
            var matrix = new MatrixD(Entity.WorldMatrix);
            matrix.Translation = target;
            return matrix;
        }
        IMyGps GetGPSbyName(string name)
        {
            var gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
            return gpsList.Find((x) => x.Name == name);
        }

        public bool IsPositionInRange(Vector3D? pos)
        {
            if (pos != null)
            {
                return (pos.Value - this.Entity.GetPosition()).LengthSquared() < MaximumRange * MaximumRange;
            }
            return false;
        }


        public List<IMyPlayer> GetPlayers()
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, (x) => IsPositionInRange(x.GetPosition()));
            return players;
        }

        public List<Sandbox.Game.Entities.MyPlanet> GetPlanetsInRange()
        {
            var planets = new List<Sandbox.Game.Entities.MyPlanet>();
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities,
                (x) => (x.GetPosition() - Entity.GetPosition()).LengthSquared() < Math.Pow(MAX_PLANET_SIZE + MaximumRange, 2));
            foreach (var entity in entities)
            {
                try
                {
                    planets.Add((Sandbox.Game.Entities.MyPlanet)entity);
                }
                catch
                {
                }
            }
            return planets;
        }

        public IMyPlayer GetPlayerByName(string name)
        {
            return GetPlayers().FirstOrDefault((x) => x.DisplayName == name);
        }

        public List<IMyPlayer> GetPlayersByPosition(Vector3D pos, double distanceSquared)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, (x) => (x.GetPosition() - pos).LengthSquared() < distanceSquared && IsPositionInRange(x.GetPosition()));
            return players;
        }

        void ShowBeamText(IMyTerminalBlock block, StringBuilder hotbarText)
        {
            hotbarText.Clear();
            hotbarText.Append("Activate Teleport");
        }


        public int GetSavedType()
        {
            int teleportationType;
            MyAPIGateway.Utilities.GetVariable<int>(Entity.EntityId.ToString() + "-TeleportationType", out teleportationType);
            return teleportationType;
        }

        public string GetSavedGPSName()
        {
            string name;
            MyAPIGateway.Utilities.GetVariable<string>(Entity.EntityId.ToString() + "-SavedGPS", out name);
            return name;
        }

        public void SendBeamMessage()
        {
            var message = new Network.MessageBeam()
            {
                EntityId = Entity.EntityId,
                ProfileNr = ProfileListbox.GetterObjects((IMyFunctionalBlock)Entity)[0]
            };
            BeamUpAll(message.ProfileNr);
            //Network.MessageUtils.SendMessageToAll(message);
        }



        void CreateUI()
        {
            List<IMyTerminalAction> actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;  
            var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
            radiusControl.Visible = ShowControlOreDetectorControls;

            new Control.Seperator<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity, "TransporterSeperator1");
            new Control.Seperator<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity, "TransporterSeperator2");

            Button = new BeamButton<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity,
                "Beam",
                "Teleport (To Profile)",
                SendBeamMessage);

            ProfileListbox = new ProfileListbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity,
                "Profiles",
                "Profiles:",
                4);

            ProfileTextbox = new ProfileTextbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity, "ProfileName", "Profile Name", "");

            SwitchControl = new FromSwitch<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity,
                "FromTo",
                "Transport from or to Target?",
                "From",
                "To");

            TargetsListbox = new TargetCombobox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity,
                "Targets",
                "Targets in range:",
                8);

            ActionBeam = new LSE.Control.ControlAction<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)Entity,
                "Beam",
                "Teleport (to Profile)",
                "Teleport (to Profile",
                SendBeamMessage);
             
        }


        public List<IMyGps> GetGPSInRange()
        {
            var gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
            gpsList.RemoveAll((x) => !IsPositionInRange(x.Coords));
            return gpsList;
        }

        public List<Teleporter> GetTransporterInRange()
        {
            var transporterInRange = new List<Teleporter>(Teleporter.TeleporterList);
            transporterInRange.RemoveAll((x) => !IsPositionInRange(x.Entity.GetPosition()) || this == x);
            return transporterInRange;
        }

        bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        public void UpdateVisual()
        {

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            foreach (var control in controls)
            {
                control.UpdateVisual();
            }
            DrawEmissive();
            ((IMyFunctionalBlock)Entity).RefreshCustomInfo();
        }

        public void LoadProfiles()
        {
            string xml = null;
            MyAPIGateway.Utilities.GetVariable<string>(Entity.EntityId + "-Profiles", out xml);
            if (xml != null)
            {
                var profileList = MyAPIGateway.Utilities.SerializeFromXML<List<Network.MessageProfile>>(xml);
                foreach (var profile in profileList)
                {
                    Profiles[profile.Profile] = profile;
                }
            }
        }

        public void SaveProfileFromOutside(Network.MessageProfile message)
        {
            Profiles[message.Profile] = message;
            var profileList = new List<Network.MessageProfile>(Profiles.Values);
            var xml = MyAPIGateway.Utilities.SerializeToXML<List<Network.MessageProfile>>(profileList);
            MyAPIGateway.Utilities.SetVariable<string>(Entity.EntityId + "-Profiles", xml);
            UpdateVisual();
        }

        public Network.MessageProfile GetProfile(int profileNr)
        {
            var profile = Profiles.GetValueOrDefault(profileNr, new Network.MessageProfile() {
                Profile = profileNr,
                ProfileName = "Profile " + profileNr.ToString()
            });
            Profiles[profileNr] = profile;
            return profile;
        }

        public void SendProfileMessage(int profileNr)
        {
            var profile = GetProfile(profileNr);
            SaveProfileFromOutside(profile);
            Network.MessageUtils.SendMessageToAll(profile);
        }
    }

    public class FromSwitch<T> : LSE.Control.SwitchControl<T>
    {
        public FromSwitch(
            IMyTerminalBlock block,
            string internalName,
            string title,
            string onButton,
            string offButton,
            bool defaultValue = true)
            : base(block, internalName, title, onButton, offButton, defaultValue)
        {
        }

        public override bool Getter(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            return profile.From;
        }

        public override void Setter(IMyTerminalBlock block, bool newState)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            profile.From = newState;
            teleporter.SendProfileMessage(profileNr);
        }
    }

    public class BeamButton<T> : LSE.Control.ButtonControl<T>
    {
        public BeamButton(
            IMyTerminalBlock block,
            string internalName,
            string title,
            Action function
            )
            : base(block, internalName, title, function)
        {

        }
    }    

    public class ProfileListbox<T> : LSE.Control.ComboboxControl<T, int>
    {
        public ProfileListbox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            int size = 5,
            List<MyTerminalControlListBoxItem> content = null)
            : base(block, internalName, title, size, false, content)
        {

        }


        public override void FillContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();

            Values[InternalName][block].Clear();

            var maxProfiles = teleporter.ProfilesAmount;
            for (var profileNr = 0; profileNr < maxProfiles; ++profileNr)
            {
                var profile = teleporter.GetProfile(profileNr);
                var item = new MyTerminalControlListBoxItem(VRage.Utils.MyStringId.GetOrCompute(profile.ProfileName),
                    VRage.Utils.MyStringId.GetOrCompute(profile.ProfileName),
                    profileNr);

                Values[InternalName][block].Add(item);
            }
            base.FillContent(block, items, selected);
        }

        
        public override void Setter(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            base.Setter(block, selected);

            teleporter.UpdateVisual();
        }
    }

    public class TargetCombobox<T> : LSE.Control.ComboboxControl<T, Network.MessageTarget>
    {
        public TargetCombobox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            int size = 5,
            List<MyTerminalControlListBoxItem> content = null)
            : base(block, internalName, title, size, true, content)
        {
        }


        public double DistanceTo(IMyTerminalBlock block, Network.MessageTarget target)
        {
            var pos = target.TargetPos;
            if (pos == null)
            {
                pos = Vector3D.Zero;
            }

            var entity = target.GetTarget();
            if (entity != null)
            {
                pos = pos.Value + entity.GetPosition();
            }

            var player = target.GetPlayerEntity();
            if (player != null)
            {
                pos = pos.Value + player.GetPosition();
            }

            return (block.GetPosition() - pos.Value).Length();
        }

        public MyTerminalControlListBoxItem TargetToItem(IMyTerminalBlock block, Network.MessageTarget target)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var from = teleporter.SwitchControl.Getter(block);

            var name = target.GetDisplayName(from);
            var distance = DistanceTo(block, target);
            return new MyTerminalControlListBoxItem(VRage.Utils.MyStringId.GetOrCompute(name),
                VRage.Utils.MyStringId.GetOrCompute((distance / 1000).ToString("0.000") + "km"),
                target);
        }

        public void AddTarget(IMyTerminalBlock block, Network.MessageTarget target)
        {
            var item = TargetToItem(block, target);
            Values[InternalName][block].Add(item);
        }

        public void AddGps(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var gpsList = teleporter.GetGPSInRange();
            foreach (var gps in gpsList)
            {
                var target = new Network.MessageTarget()
                {
                    Type = 0,
                    TargetName = gps.Name,
                    TargetPos = gps.Coords,
                };
                AddTarget(block, target);
            }
        }

        public void AddPlayers(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var players = teleporter.GetPlayers();
            foreach (var player in players)
            {
                var target = new Network.MessageTarget()
                {
                    Type = 1,
                    TargetName = player.DisplayName,
                    PlayerId = player.IdentityId,
                };
                AddTarget(block, target);
            }
        }

        public void AddTransporters(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var transporters = teleporter.GetTransporterInRange();
            foreach (var otherTransporter in transporters)
            {
                var name = ((IMyFunctionalBlock)otherTransporter.Entity).CustomName;
                var target = new Network.MessageTarget()
                {
                    Type = 2,
                    TargetName = name,
                    TargetId = otherTransporter.Entity.EntityId,
                };
                AddTarget(block, target);
            }
        }

        public void AddPlanets(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            foreach (var planet in teleporter.GetPlanetsInRange())
            {
                var target = new Network.MessageTarget()
                {
                    Type = 3,
                    TargetName = planet.Generator.Id.SubtypeId.ToString(),
                    TargetId = planet.EntityId,
                };
                AddTarget(block, target);                
            }
        }

        public override void FillContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            Values[InternalName][block].Clear();
            var from = teleporter.SwitchControl.Getter(block);
            if (from)
            {
                if (teleporter.ValidTypes.Contains(2))
                {
                    AddGps(block);
                }
                if (teleporter.ValidTypes.Contains(3))
                {
                    AddPlayers(block);
                }
                if (teleporter.ValidTypes.Contains(6))
                {
                    AddTransporters(block);
                }
            }
            else
            {
                if (teleporter.ValidTypes.Contains(4))
                {
                    AddPlanets(block);
                }
                if (teleporter.ValidTypes.Contains(0))
                {
                    AddGps(block);
                }
                if (teleporter.ValidTypes.Contains(1))
                {
                    AddPlayers(block);
                }
                if (teleporter.ValidTypes.Contains(5))
                {
                    AddTransporters(block);
                }
            }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            SetterObjects(block, profile.Targets);

            base.FillContent(block, items, selected);
        }

        void SetterObjects(IMyTerminalBlock block,
            List<Network.MessageTarget> targets)
        {
            var selected = new List<MyTerminalControlListBoxItem>();
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            foreach (var target in targets)
            {                
                var index = Values[InternalName][block].FindIndex((x) => (target.Equals(x.UserData)));
                if (index == -1)
                {
                    var item = TargetToItem(block, target);
                    Values[InternalName][block].Insert(0, item);
                    selected.Add(item);
                }
                else
                {
                    selected.Add(Values[InternalName][block][index]);
                }
            }
            base.Setter(block, selected);
        }

        public override void Setter(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var targets = new List<Network.MessageTarget>();
            foreach (var target in selected)
            {
                targets.Add((Network.MessageTarget)target.UserData);
            }

            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            profile.Targets = targets;
            teleporter.SendProfileMessage(profileNr);

            base.Setter(block, selected);

            teleporter.UpdateVisual();
        }

    }

    public class ProfileTextbox<T> : Control.Textbox<T>
    {
        public ProfileTextbox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            string defaultValue)
            : base(block, internalName, title, defaultValue)
        {
            CreateUI();
        }

        public override StringBuilder Getter(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            return new StringBuilder(profile.ProfileName);
        }

        public override void Setter(IMyTerminalBlock block, StringBuilder builder)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            profile.ProfileName = builder.ToString();
            teleporter.SendProfileMessage(profileNr);
        }
    }
}