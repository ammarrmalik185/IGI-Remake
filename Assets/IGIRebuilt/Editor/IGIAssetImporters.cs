using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace IGIRebuilt.Editor
{
    [Serializable] internal class MeshGroup { public int slot; public bool transparent; public int[] indices; }
    [Serializable] internal class PaneRenderGroup { public int group; public int[] triangles; }
    [Serializable] internal class GlassPaneDocument { public int[] indices; public PaneRenderGroup[] render; }
    [Serializable] internal class MeshDocument
    {
        public string source; public bool skinned, sector, authoredWinding; public int version;
        public float[] positions, normals, uv; public MeshGroup[] groups;
        public float[] collisionPositions; public int[] collisionIndices;
        public int[] collisionSurfaceKinds; public GlassPaneDocument[] glassPanes;
    }
    [Serializable] internal class ModelDocument
    {
        public string source, instance, model, mesh; public bool skinned; public string[] textures;
    }

    [ScriptedImporter(3, "igimesh")]
    public sealed class IGIMeshImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var data = JsonUtility.FromJson<MeshDocument>(File.ReadAllText(ctx.assetPath));
            int count = data.positions.Length / 3;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                // Version 0 was the first extraction, expressed at 100 units/metre.
                float scale = data.version >= 1 ? 1 : 100f / 4096f;
                vertices[i] = new Vector3(data.positions[i * 3], data.positions[i * 3 + 1], data.positions[i * 3 + 2]) * scale;
                normals[i] = new Vector3(data.normals[i * 3], data.normals[i * 3 + 1], data.normals[i * 3 + 2]);
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
            }
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(data.source.Split(':').Last()), indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.subMeshCount = data.groups.Length;
            for (int i = 0; i < data.groups.Length; i++)
            {
                var removed = new System.Collections.Generic.HashSet<int>((data.glassPanes ?? Array.Empty<GlassPaneDocument>()).SelectMany(p => p.render).Where(g => g.group == i).SelectMany(g => g.triangles));
                var indices = data.groups[i].indices.Where((_, index) => !removed.Contains(index / 3)).ToArray();
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                    if (!data.authoredWinding && Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normals[a] + normals[b] + normals[c]) < 0)
                        (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);
                }
                mesh.SetTriangles(indices, i);
            }
            mesh.RecalculateBounds();
            ctx.AddObjectToAsset("mesh", mesh); ctx.SetMainObject(mesh);
            var collision = new Mesh { name = mesh.name + "_collision", indexFormat = IndexFormat.UInt32 };
            if (data.collisionPositions != null && data.collisionIndices != null && (data.collisionIndices.Length > 0 || data.glassPanes != null))
            {
                collision.vertices = Enumerable.Range(0, data.collisionPositions.Length / 3).Select(i => new Vector3(data.collisionPositions[i * 3], data.collisionPositions[i * 3 + 1], data.collisionPositions[i * 3 + 2])).ToArray();
                collision.triangles = data.collisionIndices;
            }
            else
            {
                collision.vertices = vertices;
                var triangles = mesh.triangles;
                var both = new int[triangles.Length * 2]; Array.Copy(triangles, both, triangles.Length);
                for (int t = 0; t < triangles.Length; t += 3) { both[triangles.Length + t] = triangles[t]; both[triangles.Length + t + 1] = triangles[t + 2]; both[triangles.Length + t + 2] = triangles[t + 1]; }
                collision.triangles = both;
            }
            collision.RecalculateBounds(); ctx.AddObjectToAsset("collision", collision);
            if (data.collisionSurfaceKinds != null)
            {
                var surfaces = ScriptableObject.CreateInstance<ImportedSurfaceData>();
                surfaces.triangleKinds = data.collisionSurfaceKinds;
                ctx.AddObjectToAsset("surfaces", surfaces);
            }
            for (int p = 0; p < (data.glassPanes?.Length ?? 0); p++)
            {
                var pane = data.glassPanes[p];
                var physics = new Mesh { name = "glass_pane_" + p + "_physics", indexFormat = IndexFormat.UInt32 };
                var ids = pane.indices.Distinct().ToArray(); var remap = ids.Select((id, i) => (id, i)).ToDictionary(v => v.id, v => v.i);
                physics.vertices = ids.Select(id => collision.vertices[id]).ToArray();
                physics.triangles = pane.indices.Select(id => remap[id]).ToArray(); physics.RecalculateBounds();
                ctx.AddObjectToAsset("glass_physics_" + p, physics);
                var render = new Mesh { name = "glass_pane_" + p + "_render", indexFormat = IndexFormat.UInt32 };
                var selected = pane.render.SelectMany(g => g.triangles.SelectMany(t => data.groups[g.group].indices.Skip(t * 3).Take(3))).Distinct().ToArray();
                var map = selected.Select((id, i) => (id, i)).ToDictionary(v => v.id, v => v.i);
                render.vertices = selected.Select(id => vertices[id]).ToArray(); render.normals = selected.Select(id => normals[id]).ToArray(); render.uv = selected.Select(id => uv[id]).ToArray();
                render.subMeshCount = data.groups.Length;
                for (int g = 0; g < data.groups.Length; g++)
                    render.SetTriangles(pane.render.Where(group => group.group == g).SelectMany(group => group.triangles.SelectMany(t => data.groups[g].indices.Skip(t * 3).Take(3))).Select(id => map[id]).ToArray(), g);
                render.RecalculateBounds(); ctx.AddObjectToAsset("glass_render_" + p, render);
            }
        }
    }

    [ScriptedImporter(6, "igimodel", 100)]
    public sealed class IGIModelImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var data = JsonUtility.FromJson<ModelDocument>(File.ReadAllText(ctx.assetPath));
            ctx.DependsOnArtifact(data.mesh);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(data.mesh);
            if (mesh == null) throw new InvalidDataException("Missing mesh: " + data.mesh);
            var geometry = JsonUtility.FromJson<MeshDocument>(File.ReadAllText(data.mesh));
            var go = new GameObject(data.instance);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[geometry.groups.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var group = geometry.groups[i];
                string shaderPath = "Assets/IGIRebuilt/" + (group.transparent ? "ImportedTransparent.shader" : "ImportedSurface.shader");
                ctx.DependsOnArtifact(shaderPath);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null) throw new InvalidDataException("Missing imported surface shader: " + shaderPath);
                var material = new Material(shader) { name = "Slot " + group.slot };
                material.SetInt("_Cull", geometry.sector ? (int)CullMode.Back : (int)CullMode.Off);
                material.SetFloat("_Glossiness", 0.1f);
                if (group.slot >= 0 && group.slot < data.textures.Length && !string.IsNullOrEmpty(data.textures[group.slot]))
                {
                    string texture = data.textures[group.slot];
                    ctx.DependsOnArtifact(texture);
                    material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
                    if (material.mainTexture == null) ctx.LogImportWarning("Missing texture: " + texture);
                    if (!group.transparent)
                    {
                        material.SetFloat("_Cutoff", 0.25f);
                    }
                }
                materials[i] = material; ctx.AddObjectToAsset("material" + i, material);
            }
            renderer.sharedMaterials = materials;
            var assets = AssetDatabase.LoadAllAssetsAtPath(data.mesh);
            var surfaceData = assets.OfType<ImportedSurfaceData>().FirstOrDefault();
            if (surfaceData != null) go.AddComponent<BulletSurface>().data = surfaceData;
            for (int p = 0; p < (geometry.glassPanes?.Length ?? 0); p++)
            {
                var pane = new GameObject("Glass pane " + p); pane.transform.SetParent(go.transform, false); pane.layer = DemoWorld.WorldLayer;
                pane.AddComponent<MeshFilter>().sharedMesh = assets.OfType<Mesh>().First(m => m.name == "glass_pane_" + p + "_render");
                pane.AddComponent<MeshRenderer>().sharedMaterials = materials;
                pane.AddComponent<MeshCollider>().sharedMesh = assets.OfType<Mesh>().First(m => m.name == "glass_pane_" + p + "_physics");
                pane.AddComponent<BulletSurface>().kind = BulletSurfaceKind.Glass;
            }
            var info = go.AddComponent<ImportedModelInfo>();
            info.source = data.source; info.instance = data.instance; info.originalModel = data.model; info.requiresSkinning = data.skinned;
            ctx.AddObjectToAsset("model", go); ctx.SetMainObject(go);
        }
    }

    [ScriptedImporter(1, "igimission")]
    public sealed class IGIMissionImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var mission = ScriptableObject.CreateInstance<ImportedMissionData>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(ctx.assetPath), mission);
            mission.name = mission.number.ToString("00") + " - " + mission.title;
            ctx.AddObjectToAsset("mission", mission); ctx.SetMainObject(mission);
        }
    }

    [Serializable] internal class WeaponDocument
    {
        public string displayName, description, modelPath, fireSoundPath, source;
        public int category, magazineSize;
        public float roundsPerMinute, reloadSeconds, range, modelLength, spreadDegrees, recoilDegrees;
        public bool automatic;
    }
    [ScriptedImporter(1, "igiweapon", 200)]
    public sealed class IGIWeaponImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var data = JsonUtility.FromJson<WeaponDocument>(File.ReadAllText(ctx.assetPath));
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = weapon.displayName = data.displayName;
            if (!string.IsNullOrEmpty(data.modelPath))
            {
                ctx.DependsOnArtifact(data.modelPath);
                weapon.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.modelPath);
            }
            if (!string.IsNullOrEmpty(data.fireSoundPath))
            {
                ctx.DependsOnArtifact(data.fireSoundPath);
                weapon.fireSound = AssetDatabase.LoadAssetAtPath<AudioClip>(data.fireSoundPath);
            }
            weapon.magazineSize = Math.Max(1, data.magazineSize);
            weapon.reserveAmmo = weapon.magazineSize * 5;
            weapon.secondsPerShot = data.roundsPerMinute > 0 ? 60 / data.roundsPerMinute : 1;
            weapon.reloadSeconds = Mathf.Max(0.1f, data.reloadSeconds);
            weapon.automatic = data.automatic;
            weapon.range = Mathf.Max(1, data.range);
            weapon.modelLength = Mathf.Max(0.05f, data.modelLength);
            weapon.spreadDegrees = data.spreadDegrees;
            weapon.recoilDegrees = data.recoilDegrees;
            // The prototype has one hitscan damage value; retail calibre/projectile rules remain in the source definitions.
            ctx.AddObjectToAsset("weapon", weapon); ctx.SetMainObject(weapon);
        }
    }
}
