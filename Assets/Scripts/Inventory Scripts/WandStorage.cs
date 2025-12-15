using UnityEngine;

[System.Serializable] // 关键：加上这个，才能在 Inspector 里看到折叠菜单
public class WandStorage
{
    public string storageName = "Default Wand"; // 给仓库起个名，方便调试

    // 仓库里的具体内容
    public MagicItem originalMagic;
    public MagicItem modifiedMagic1;
    public MagicItem modifiedMagic2;
    public MagicItem triggerMagic;

    // 一个方便的方法：检查这个仓库是不是空的
    public bool IsEmpty()
    {
        return originalMagic == null;
    }

    // 清空仓库
    public void Clear()
    {
        originalMagic = null;
        modifiedMagic1 = null;
        modifiedMagic2 = null;
        triggerMagic = null;
    }
}