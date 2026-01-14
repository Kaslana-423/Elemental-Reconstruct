using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Relic", menuName = "Relic System/Relic Data")]
public class RelicData : ScriptableObject
{
    public string itemID; // 唯一标识符
    [Header("基础信息")]
    public string relicName;
    [TextArea] public string description;
    public Sprite icon;
    public int rarity; // 1-普通，2-稀有，3-史诗，4-传说

    [Header("遗物效果列表")]
    // 一个遗物可能有多个效果（比如：既加血又加攻）
    public List<RelicEffect> effects;
}