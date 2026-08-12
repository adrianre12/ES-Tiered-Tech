using ObjectBuilders.SafeZone;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Definitions.SafeZone;
using SpaceEngineers.Game.ModAPI;
using System;
using TieredTechBlocks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ES.ZoneBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SafeZoneBlock), false, "SafeZoneBlock", "SafeZoneBlock2x", "SafeZoneBlock4x", "SafeZoneBlock8x", "SafeZoneBlockReskin", "SafeZoneBlockReskin2x", "SafeZoneBlockReskin4x", "SafeZoneBlockReskin8x")]
    public class ESZoneBlock : MyGameLogicComponent
    {
        IMySafeZoneBlock block;

        public const int PollPeriod = 6; //9.6s

        public static Guid ModStorageKey = new Guid("154045E8-9AF6-4C58-B120-F059E56651B5");
        private static readonly MyDefinitionId definitionZoneChip = new MyDefinitionId(typeof(MyObjectBuilder_Component), "ZoneChip");

        private int updateCounter = 0;
        private long offlineS;
        private long minOfflineMins;
        private long testOfflineS;
        private bool forceOff = false;
        private long offlineUpkeepMultiplier;


        [ProtoContract(UseProtoMembersOnly = true)]
        internal class ModStorage
        {
            [ProtoMember(1)]
            public long LastTime;

            public ModStorage()
            {
                LastTime = 0;
            }
        }

        internal ModStorage Storage = new ModStorage();
        private uint safeZoneUpkeep;
        private long safeZoneUpkeepTimeS;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            Log.Msg("Init...");
            block = Entity as IMySafeZoneBlock;
            Config.Load();
            minOfflineMins = Config.Instance.MinOfflineMins;
            Log.Debug = Config.Instance.Debug;
            forceOff = Config.Instance.ForceOff;
            testOfflineS = Config.Instance.TestOfflineS;
            offlineUpkeepMultiplier = Config.Instance.OfflineUpkeepMultiplier;

            if (Log.Debug) Log.Msg($"minOfflineMins={minOfflineMins} testOfflineS={testOfflineS}");

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {

            if (block.CubeGrid?.Physics == null || !MyAPIGateway.Session.IsServer)
                return;

            if (block.Storage == null)
                block.Storage = new MyModStorageComponent();

            LoadFromModStorage();

            MySafeZoneBlockDefinition bd = (MySafeZoneBlockDefinition)MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition);

            safeZoneUpkeep = bd.SafeZoneUpkeep;
            safeZoneUpkeepTimeS = bd.SafeZoneUpkeepTimeM * 60 * offlineUpkeepMultiplier;
            if (Log.Debug) Log.Msg($"safeZoneUpkeep={safeZoneUpkeep} safeZoneUpkeepTimeS={safeZoneUpkeepTimeS}");

            if (testOfflineS > 0)
            {
                Storage.LastTime = DateTime.Now.Ticks / TimeSpan.TicksPerSecond - testOfflineS;
                SaveToModStorage();
            }


            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            if (--updateCounter > 0)
                return;
            updateCounter = PollPeriod;

            if (Paused())
            {
                if (Log.Debug) Log.Msg("Paused detected");

                if (!block.Enabled || !block.IsSafeZoneEnabled())
                    return;

                int upkeep = safeZoneUpkeepTimeS > 0 ? (int)(offlineS * 1 / safeZoneUpkeepTimeS * safeZoneUpkeep) : 0;

                if (upkeep > 0)
                {
                    int remaining = ConsumeUpkeep(upkeep);
                    if (remaining == 0)
                    {
                        Log.Msg($"Grid '{block.CubeGrid.CustomName}' ZoneChips consumed={upkeep}");
                    }
                    else
                    {
                        if (forceOff)
                            block.EnableSafeZone(false);
                        Log.Msg($"Grid '{block.CubeGrid.CustomName}' Not enough ZoneChips needed={upkeep}, missing={remaining}");
                    }
                }
            }

        }

        internal bool Paused()
        {
            long nowS = DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
            offlineS = 0;
            LoadFromModStorage();

            if (Storage.LastTime != 0)
                offlineS = nowS - Storage.LastTime;
            Storage.LastTime = nowS;

            SaveToModStorage();

            if (Log.Debug) Log.Msg($"offlineS = {offlineS}");

            return offlineS > minOfflineMins * 60;
        }

        private int ConsumeUpkeep(int upkeep)
        {
            if (Log.Debug) Log.Msg($"upkeep={upkeep} inventoryCount={block.InventoryCount}");
            IMyInventory zoneInv = block.GetInventory(0);
            if (zoneInv == null)
            {
                if (Log.Debug) Log.Msg("zoneInv=null");
                return 0;
            }

            int remaining = upkeep;
            int removed = 0;

            var myInv = (MyInventory)zoneInv;
            var x = myInv.RemoveItemsOfType(remaining, definitionZoneChip);
            removed = (int)x;
            remaining -= removed;
            if (Log.Debug) Log.Msg($"zoneBlock removed={removed} x={x}");

            if (remaining <= 0)
                return 0;

            foreach (var container in block.CubeGrid.GetFatBlocks<IMyCargoContainer>())
            {
                if (!container.IsFunctional)
                    continue;
                var inventory = container.GetInventory();
                if (!zoneInv.IsConnectedTo(inventory))
                    continue;

                removed = (int)((MyInventory)inventory).RemoveItemsOfType(remaining, definitionZoneChip, MyItemFlags.None, false);
                if (Log.Debug) Log.Msg($"container '{container.CustomName}' removed={removed}");

                remaining -= removed;
                if (remaining <= 0)
                    return 0;
            }

            return remaining;
        }

        internal void SaveToModStorage()
        {
            try
            {
                Entity.Storage[ModStorageKey] = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Storage));
            }
            catch (Exception e)
            {
                Log.Msg($"Error Saving ModStorage\n {e}");
            }
        }

        internal void LoadFromModStorage()
        {
            try
            {
                Storage = new ModStorage();
                string tmp;
                if (Entity.Storage.TryGetValue(ModStorageKey, out tmp))
                {
                    Storage = MyAPIGateway.Utilities.SerializeFromBinary<ModStorage>(Convert.FromBase64String(tmp));
                }
                else
                {
                    Log.Msg($"Failed to load ModStorage, creating new.");
                    Storage = new ModStorage();
                    SaveToModStorage();
                }
            }
            catch (Exception e)
            {
                Log.Msg($"Error loading ModStorage\n {e}");
                Storage = new ModStorage();
            }
        }

    }
}
