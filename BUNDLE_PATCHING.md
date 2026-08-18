# Patching assetbundles on export

The exporter package now supports patching an AssetBundle to replace references. By doing so it is 
possible to use in-game resources (assets or scripts) instead of custom ones. This is specially 
relevant to avoid bloating the game with the same materials over and over.

The Authoring package provides the most common patches and ready to use resources for this, but 
you can add additional patches in your mod.

## Getting access to the patcher

Make an Editor/ folder anywhere in your Assets folder and add an assembly definition inside. 
You will need to add a reference to 'stationeers.modding.exporter' to get access to the patching interfaces.

## Adding resource patches (GameObjects/Materials/Sprites/Meshes)

Create an Editor script like this:

```csharp
namespace YourASMDEF.Editor
{
    internal sealed class GameObjectReferenceProvider : IAssetReferencePatchProvider
    {
        public void CollectPatches(IAssetReferencePatchCollector collector)
        {
            // replace local GameObject with an ingame game Object.
            collector.Add(
                new AssetReferencePatch(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CustomItemDrill.prefab"),
                    "resources.assets", 37119, 1,    // Game reference from <Asset name="ItemDrill" file="resources.assets" pathId="37119" typeId="1" offset="388328288" size="135" />
                    AssetReferencePatchCleanup.RemoveIfUnreferenced // Remove/keep custom proxy asset in the bundle
                )
            );
        }
    }
}
```

## Adding monoscript patches (Monobehaviors)

Create an Editor script like this:


```csharp
using stationeers.modding.exporter;
using UnityEngine;

namespace YourASMDEF.Editor
{
    internal sealed class MonoScriptReferenceProvider : IMonoScriptReferencePatchProvider
    {
        public void CollectPatches(IMonoScriptReferencePatchCollector collector)
        {
            collector.Add(
                new MonoScriptReferencePatch(
                    typeof(YourModNamespace.YourThingProxyClass),
                    "globalgamemanagers.assets", 2822, //     <MonoScript assembly="Assembly-CSharp" namespace="Assets.Scripts.Objects" class="Item" name="Item" file="globalgamemanagers.assets" pathId="2822" classId="115" kind="MonoBehaviour" baseType="Assets.Scripts.Objects.DynamicThing" unityVersion="2022.3.62f3" />
                    "Assembly-CSharp", "Assets.Scripts.Objects", "Item"
                )
            );
        }
    }
}
```
you can add as many references as you need with additional calls to collector.Add()

## Finding resources and monoscript in-game references

They are available in the modding discord.

## Disabling reference patching

Go to project settings, under 'Stationeers Mods Exporting' you will find two new settings:
- Path asset references
- Path MonoScript references