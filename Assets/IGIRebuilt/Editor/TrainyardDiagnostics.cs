using System;
using UnityEditor;
using UnityEngine;
namespace IGIRebuilt.Editor {
 public static class TrainyardDiagnostics {
  public static void Inspect() {
   string[] paths = { "Assets/IGIImported/Textures/missions/location0/level1/textures/605_02_1.png", "Assets/IGIImported/Textures/missions/location0/level1/terrain/terrain/000.png", "Assets/IGIImported/Meshes/8CD3437EA52B0B74696F6425B921D7ABD0F1435164B8F3CB38C02F9EA88F6A19.igimesh" };
   foreach(string path in paths) {
    var importer=AssetImporter.GetAtPath(path);
    var main=AssetDatabase.LoadMainAssetAtPath(path);
    Debug.Log("ASSET_DIAG " + path + " importer=" + (importer == null ? "null" : importer.GetType().FullName) + " declared=" + AssetDatabase.GetMainAssetTypeAtPath(path) + " main=" + (main == null ? "null" : main.GetType().FullName) + " all=" + AssetDatabase.LoadAllAssetsAtPath(path).Length);
    if(importer is TextureImporter texture) Debug.Log("TEXTURE_SETTINGS " + EditorJsonUtility.ToJson(texture));
    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    var after=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    Debug.Log("ASSET_AFTER " + path + " texture=" + (after == null ? "null" : after.width + "x" + after.height));
   }
  }
 }
}
