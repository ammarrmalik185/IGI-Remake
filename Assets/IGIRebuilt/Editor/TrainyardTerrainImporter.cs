using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace IGIRebuilt.Editor
{
    [Serializable] internal class GroundLayer { public int tile; public bool overlay, detail; public int[] indices; }
    [Serializable] internal class GroundChunk { public Vector3 position; public float[] positions, uv, alpha; public GroundLayer[] layers; }
    [Serializable] internal class GroundDocument { public GroundChunk[] chunks; }
    [ScriptedImporter(3, "igiground", 100)]
    public sealed class TrainyardTerrainImporter : ScriptedImporter
    {
        private sealed class Batch
        {
            public int tile; public bool overlay, detail;
            public readonly List<Vector3> positions = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int> indices = new List<int>();
        }
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var data = JsonUtility.FromJson<GroundDocument>(File.ReadAllText(ctx.assetPath));
            var batches = new Dictionary<string, Batch>();
            foreach (var chunk in data.chunks)
                foreach (var layer in chunk.layers)
                {
                    string key = layer.tile + (layer.detail ? " detail" : layer.overlay ? " overlay" : " base");
                    if (!batches.TryGetValue(key, out var batch)) { batch = new Batch { tile = layer.tile, overlay = layer.overlay, detail = layer.detail }; batches.Add(key, batch); }
                    var remap = new Dictionary<int, int>();
                    foreach (int old in layer.indices)
                    {
                        if (!remap.TryGetValue(old, out int index))
                        {
                            index = batch.positions.Count; remap.Add(old, index);
                            batch.positions.Add(new Vector3(chunk.positions[old * 3], chunk.positions[old * 3 + 1], chunk.positions[old * 3 + 2]) + chunk.position);
                            batch.uv.Add(new Vector2(chunk.uv[old * 2], chunk.uv[old * 2 + 1]));
                            batch.colors.Add(new Color(1, 1, 1, chunk.alpha[old]));
                        }
                        batch.indices.Add(index);
                    }
                }
            var root = new GameObject("Trainyard original terrain");
            foreach (var pair in batches)
            {
                var batch = pair.Value;
                var mesh = new Mesh { name = pair.Key, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(batch.positions); mesh.SetUVs(0, batch.uv); mesh.SetColors(batch.colors);
                mesh.SetTriangles(batch.indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string texturePath = "Assets/IGIImported/Textures/missions/location0/level1/terrain/terrain/" + batch.tile.ToString("000") + ".png";
                string shaderPath = "Assets/IGIRebuilt/Trainyard/" + (batch.detail ? "TerrainDetail.shader" : batch.overlay ? "TerrainOverlay.shader" : "TerrainBase.shader");
                ctx.DependsOnArtifact(texturePath); ctx.DependsOnArtifact(shaderPath);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null) throw new InvalidDataException("Terrain shader missing: " + shaderPath);
                var mat = new Material(shader) { name = pair.Key, mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) };
                if (mat.mainTexture == null) throw new InvalidDataException("Terrain tile missing: " + texturePath);
                ctx.AddObjectToAsset("material " + pair.Key, mat); ctx.AddObjectToAsset("mesh " + pair.Key, mesh);
                var go = new GameObject(pair.Key); go.layer = DemoWorld.WorldLayer; go.isStatic = true;
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (!batch.overlay) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            ctx.AddObjectToAsset("terrain", root); ctx.SetMainObject(root);
        }
    }
}
