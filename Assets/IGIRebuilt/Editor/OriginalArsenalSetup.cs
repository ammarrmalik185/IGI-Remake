using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    public static class OriginalArsenalSetup
    {
        public static readonly string[] Names = { "GLOCK", "MP5SD", "KNIFE", "COLTANACONDA", "DESERTEAGLE", "UZI", "UZIX2", "AK47", "M16A2", "SPAS12", "JACKHAMMER", "MINIMI", "DRAGUNOV", "RPG18", "GRENADE", "FLASHBANG", "PROXIMITYMINE", "BINOCULARS", "MEDIPACK" };

        [MenuItem("Tools/IGI Rebuilt/Add original player arsenal")]
        public static void Apply()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ProjectSetup.ConfigurationPath);
            if (config == null) throw new Exception("Create the mission configuration first.");
            config.weapons = Create();
            var guardAudio = AssetDatabase.LoadAssetAtPath<GuardPresentationSettings>("Assets/IGIRebuilt/Resources/GuardPresentationSettings.asset");
            if (guardAudio != null)
            {
                guardAudio.shot = Array.Find(config.weapons, w => w.name == "AK47").fireSound;
                EditorUtility.SetDirty(guardAudio);
            }
            config.briefing = config.briefing.Replace("1 Glock   2 MP5 SD3   3 Knife", "1 Glock / 2 MP5 / 3 Knife; mouse wheel: full arsenal");
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        public static WeaponDefinition[] Create()
        {
            const string folder = "Assets/IGIRebuilt/Data/TrainyardWeapons";
            Directory.CreateDirectory(folder);
            var weapons = new WeaponDefinition[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string id = Names[i];
                var source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/IGIImported/Weapons/" + id + ".igiweapon");
                if (source == null || source.visualPrefab == null) throw new Exception("Missing original weapon model: " + id);
                string path = folder + "/" + id + ".asset";
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (weapon == null) { weapon = ScriptableObject.CreateInstance<WeaponDefinition>(); AssetDatabase.CreateAsset(weapon, path); }
                EditorUtility.CopySerialized(source, weapon);
                weapon.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIRebuilt/Data/WeaponViews/" + id + ".igiweaponview");
                if (weapon.visualPrefab == null) throw new Exception("Missing complete weapon view " + id + ". Export with: dotnet run --project tools/TrainyardExport -- . --weapon-views");
                weapon.name = id;
                weapon.damage = id == "GLOCK" ? 25 : id == "MP5SD" ? 24 : id == "KNIFE" ? 100 : id == "DRAGUNOV" ? 100 : id == "COLTANACONDA" || id == "DESERTEAGLE" ? 50 : 34;
                weapon.spreadDegrees = Mathf.Abs(weapon.spreadDegrees);
                weapon.viewRotation = Vector3.zero;
                weapon.modelLength = id == "GLOCK" ? .24f : id == "KNIFE" ? .28f : source.modelLength;
                weapon.viewPosition = weapon.modelLength > .5f ? new Vector3(.22f, -.21f, .64f) : new Vector3(.19f, -.17f, .46f);
                weapon.melee = id == "KNIFE";
                weapon.dualWield = id == "UZIX2";
                weapon.canZoom = id == "MP5SD" || id == "DRAGUNOV" || id == "BINOCULARS";
                weapon.scoped = id == "DRAGUNOV";
                weapon.viewHorizontalFieldOfView = id == "RPG18" ? 82 : id == "MINIMI" || id == "JACKHAMMER" ? 78 :
                    id == "UZIX2" || id == "DRAGUNOV" || id == "M16A2" ? 74 : source.modelLength > .5f ? 70 : 66;
                weapon.fireSound = WeaponShotAudioSetup.SingleRound(id, source.fireSound, weapon.secondsPerShot);
                if (weapon.scoped)
                {
                    weapon.scopeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/IGIRebuilt/WeaponScope.shader");
                    const string sprites = "Assets/IGIImported/Textures/common/sprites/";
                    weapon.scopeMask = AssetDatabase.LoadAssetAtPath<Texture2D>(sprites + "snipersight.png");
                    weapon.scopeHorizontal = AssetDatabase.LoadAssetAtPath<Texture2D>(sprites + "snipersight_x.png");
                    weapon.scopeVertical = AssetDatabase.LoadAssetAtPath<Texture2D>(sprites + "snipersight_y.png");
                    if (weapon.scopeShader == null || weapon.scopeMask == null || weapon.scopeHorizontal == null || weapon.scopeVertical == null)
                        throw new Exception("Missing original Dragunov scope assets.");
                }
                weapon.noiseRadius = weapon.melee ? 3 : id == "MP5SD" ? 18 : 100;
                string audio = id == "AK47" ? "ak47" : id == "GLOCK" || id == "DESERTEAGLE" ? "glock" : id == "COLTANACONDA" ? "colt" : id == "DRAGUNOV" ? "dragu" : id == "JACKHAMMER" ? "jackh" : id == "M16A2" ? "m16" : id == "MP5SD" ? "mp5" : id.ToLowerInvariant();
                weapon.reloadSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IGIImported/Audio/" + audio + "_reload_1.wav");
                if (weapon.melee) { weapon.range = 1.9f; weapon.reserveAmmo = 0; }
                if (id == "SPAS12" || id == "JACKHAMMER") { weapon.pellets = 8; weapon.damage = 12; weapon.spreadDegrees = id == "SPAS12" ? 5 : 6; }
                if (id == "DRAGUNOV") { weapon.aimFieldOfView = 15; weapon.spreadDegrees = .15f; }
                if (id == "RPG18") { weapon.action = WeaponAction.Rocket; weapon.damage = 180; weapon.blastRadius = 8; weapon.reserveAmmo = 3; weapon.projectileSpeed = 60; }
                if (id == "GRENADE" || id == "FLASHBANG") { weapon.action = id == "GRENADE" ? WeaponAction.Grenade : WeaponAction.Flashbang; weapon.damage = 140; weapon.projectileSpeed = 14; weapon.reserveAmmo = 4; weapon.blastRadius = id == "GRENADE" ? 7 : 12; weapon.noiseRadius = 18; }
                if (id == "PROXIMITYMINE") { weapon.action = WeaponAction.ProximityMine; weapon.damage = 180; weapon.reserveAmmo = 3; weapon.blastRadius = 7; weapon.noiseRadius = 3; }
                if (id == "BINOCULARS") { weapon.action = WeaponAction.Binoculars; weapon.aimFieldOfView = 12; weapon.noiseRadius = 0; }
                if (id == "MEDIPACK") { weapon.action = WeaponAction.Medipack; weapon.reserveAmmo = 2; weapon.noiseRadius = 0; }
                if (weapon.action == WeaponAction.Rocket || weapon.action == WeaponAction.Grenade || weapon.action == WeaponAction.ProximityMine)
                    weapon.explosionSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IGIImported/Audio/" + (id == "PROXIMITYMINE" ? "explo_proxy.wav" : "explo_02_m.wav"));
                if (id == "FLASHBANG") weapon.explosionSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IGIImported/Audio/explo_flash.wav");
                if (weapon.action == WeaponAction.Rocket || weapon.action == WeaponAction.Grenade || weapon.action == WeaponAction.ProximityMine)
                    weapon.explosionEffect = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IGIRebuilt/Prefabs/CameraExplosion.prefab");
                EditorUtility.SetDirty(weapon);
                weapons[i] = weapon;
            }
            return weapons;
        }
    }
}
