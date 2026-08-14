using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TieredTechBlocks
{
    [ProtoContract]
    [Serializable]
    public class MyConfig
    {
        [ProtoMember(1)]
        public Item SmallGridCommon;
        [ProtoMember(2)]
        public Item LargeGridCommon;
        [ProtoMember(3)]
        public Item SmallGridRare;
        [ProtoMember(4)]
        public Item LargeGridRare;
        [ProtoMember(5)]
        public Item SmallGridExotic;
        [ProtoMember(6)]
        public Item LargeGridExotic;
        [ProtoMember(7)]
        public List<string> ExcludeGrids;
        [ProtoMember(8)]
        public Boolean DisableGrindSubgridDamage = true;
        [ProtoMember(9)]
        public Boolean Debug = false;
        [ProtoMember(10)]
        public uint MinOfflineMins = 15; // offline time to ignore
        [ProtoMember(11)]
        public uint TestOfflineS = 0; // use to spoof offline periods
        [ProtoMember(12)]
        public Boolean ForceOff = true;
        [ProtoMember(13)]
        public Boolean EnableOfflineUpkeep = true;
    }

    [ProtoContract]
    [Serializable]
    public class Item
    {
        [XmlAttribute]
        public float Chance;
        [XmlAttribute]
        public int MinAmount;
        [XmlAttribute]
        public int MaxAmount;
    }
}