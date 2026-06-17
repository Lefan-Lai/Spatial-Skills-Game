using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeonautRescue
{
    public sealed class ShapeonautBlockHandle : MonoBehaviour
    {
        public string Id;
    }

    public sealed class ShapeonautWorldView
    {
        public const float CellSize = 0.74f;
        public const float LayerHeight = 0.52f;

        private readonly Dictionary<BlockKind, BlockDefinition> definitions;
        private readonly Dictionary<BlockKind, Material> blockMaterials = new Dictionary<BlockKind, Material>();
        private readonly List<Transform> zoneMarkers = new List<Transform>();

        private Camera camera;
        private Transform root;
        private Transform environmentRoot;
        private Transform buildRoot;
        private Transform ghostRoot;
        private Transform novaRoot;
        private Transform pipRoot;

        private Material grassMaterial;
        private Material dirtMaterial;
        private Material waterMaterial;
        private Material gridMaterial;
        private Material ghostMaterial;
        private Material highlightMaterial;
        private Material whiteMaterial;
        private Material darkMaterial;

        private float cameraYaw = 42f;
        private float cameraPitch = 48f;
        private float cameraDistance = 10.2f;
        private Vector3 cameraTarget = Vector3.zero;
        private bool orbiting;
        private Vector3 orbitMouse;

        public ShapeonautWorldView(Dictionary<BlockKind, BlockDefinition> definitions)
        {
            this.definitions = definitions;
        }

        public Camera MainCamera
        {
            get { return camera; }
        }

        public Vector3 NovaPosition
        {
            get { return novaRoot != null ? novaRoot.position : Vector3.zero; }
        }

        public void Initialize(List<LevelDefinition> levels)
        {
            SetupCameraAndLight();
            SetupMaterials();
            BuildWorld(levels);
            ApplyExplorationCamera(true);
        }

        public void UpdateExploration(float deltaTime)
        {
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                move.z += 1f;
            }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                move.z -= 1f;
            }
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                move.x -= 1f;
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                move.x += 1f;
            }

            if (move.sqrMagnitude > 0.01f)
            {
                move.Normalize();
                novaRoot.position += move * deltaTime * 2.6f;
                novaRoot.position = new Vector3(Mathf.Clamp(novaRoot.position.x, -5.8f, 5.8f), 0.18f, Mathf.Clamp(novaRoot.position.z, -4.2f, 4.2f));
                novaRoot.rotation = Quaternion.LookRotation(move, Vector3.up);
            }

            if (pipRoot != null && novaRoot != null)
            {
                Vector3 follow = novaRoot.position + new Vector3(-0.55f, 0.35f + Mathf.Sin(Time.time * 3f) * 0.05f, -0.38f);
                pipRoot.position = Vector3.Lerp(pipRoot.position, follow, deltaTime * 5f);
            }

            HandleOrbitInput(false);
            cameraTarget = Vector3.Lerp(cameraTarget, novaRoot.position + Vector3.up * 0.65f, deltaTime * 4f);
            ApplyCamera();
        }

        public void UpdateBuildCamera()
        {
            HandleOrbitInput(true);
            ApplyCamera();
        }

        public int GetNearestZoneIndex(List<LevelDefinition> levels)
        {
            int best = -1;
            float bestDistance = 1.35f;
            for (int i = 0; i < zoneMarkers.Count && i < levels.Count; i++)
            {
                float distance = Vector3.Distance(novaRoot.position, zoneMarkers[i].position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        public void ApplyExplorationCamera(bool instant)
        {
            cameraYaw = 42f;
            cameraPitch = 48f;
            cameraDistance = 10.2f;
            cameraTarget = novaRoot != null ? novaRoot.position + Vector3.up * 0.65f : Vector3.zero;
            ApplyCamera();
        }

        public void ApplyBuildCamera(int mode)
        {
            cameraTarget = new Vector3(0f, 0.72f, 0f);
            if (mode == 1)
            {
                cameraYaw = 45f;
                cameraPitch = 78f;
                cameraDistance = 8.8f;
            }
            else if (mode == 2)
            {
                cameraYaw = 0f;
                cameraPitch = 45f;
                cameraDistance = 8.4f;
            }
            else if (mode == 3)
            {
                cameraYaw = 90f;
                cameraPitch = 45f;
                cameraDistance = 8.4f;
            }
            else
            {
                cameraYaw = 43f;
                cameraPitch = 48f;
                cameraDistance = 9.4f;
            }

            ApplyCamera();
        }

        public void RebuildBlocks(List<PlacedBlock> blocks, Vector3Int highlightCell)
        {
            ClearChildren(buildRoot);
            for (int i = 0; i < blocks.Count; i++)
            {
                PlacedBlock block = blocks[i];
                Material material = block.Cell == highlightCell ? highlightMaterial : blockMaterials[block.Kind];
                GameObject view = CreateBlockObject(block.Kind, block.Cell, block.Rotation, buildRoot, material, true, block.Id);
                block.View = view;
            }
        }

        public void RebuildGhosts(LevelDefinition level, List<PlacedBlock> blocks)
        {
            ClearChildren(ghostRoot);
            for (int i = 0; i < level.Targets.Count; i++)
            {
                TargetBlock target = level.Targets[i];
                if (HasMatchingBlock(target, blocks))
                {
                    continue;
                }

                CreateBlockObject(target.Kind, target.Cell, target.Rotation, ghostRoot, ghostMaterial, false, "");
            }
        }

        public bool TryRaycastGrid(Vector3 mouse, int layer, out Vector3Int cell)
        {
            Ray ray = camera.ScreenPointToRay(mouse);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                cell = WorldToCell(hit);
                cell.y = layer;
                return true;
            }

            cell = Vector3Int.zero;
            return false;
        }

        public string RaycastBlockId(Vector3 mouse)
        {
            Ray ray = camera.ScreenPointToRay(mouse);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                ShapeonautBlockHandle handle = hit.collider.GetComponentInParent<ShapeonautBlockHandle>();
                if (handle != null)
                {
                    return handle.Id;
                }
            }

            return "";
        }

        public Vector3 CellToWorld(Vector3Int cell)
        {
            return new Vector3(cell.x * CellSize, cell.y * LayerHeight, cell.z * CellSize);
        }

        public Vector3Int WorldToCell(Vector3 world)
        {
            return new Vector3Int(Mathf.RoundToInt(world.x / CellSize), 0, Mathf.RoundToInt(world.z / CellSize));
        }

        public void SetZoneRepaired(int levelIndex, bool repaired)
        {
            if (levelIndex < 0 || levelIndex >= zoneMarkers.Count)
            {
                return;
            }

            Renderer renderer = zoneMarkers[levelIndex].GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = repaired ? MakeMaterial(new Color(0.5f, 0.92f, 0.58f), 0.02f, 0.5f) : MakeMaterial(new Color(0.32f, 0.65f, 0.95f), 0.02f, 0.35f);
            }
        }

        private void SetupCameraAndLight()
        {
            camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.58f, 0.78f, 0.9f);
            camera.fieldOfView = 39f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 160f;

            Light light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                light = new GameObject("Warm Planet Sun").AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.96f, 0.84f);
            light.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.68f, 0.76f, 0.82f);
        }

        private void SetupMaterials()
        {
            grassMaterial = MakeMaterial(new Color(0.42f, 0.66f, 0.34f), 0.02f, 0.45f);
            dirtMaterial = MakeMaterial(new Color(0.44f, 0.32f, 0.22f), 0.02f, 0.28f);
            waterMaterial = MakeTransparentMaterial(new Color(0.22f, 0.58f, 0.78f, 0.62f), 0.02f, 0.75f);
            gridMaterial = MakeTransparentMaterial(new Color(1f, 1f, 1f, 0.28f), 0f, 0.1f);
            ghostMaterial = MakeTransparentMaterial(new Color(0.45f, 0.84f, 1f, 0.35f), 0f, 0.15f);
            highlightMaterial = MakeTransparentMaterial(new Color(1f, 0.42f, 0.36f, 0.72f), 0f, 0.25f);
            whiteMaterial = MakeMaterial(new Color(0.92f, 0.96f, 1f), 0.02f, 0.55f);
            darkMaterial = MakeMaterial(new Color(0.04f, 0.1f, 0.14f), 0f, 0.4f);

            foreach (KeyValuePair<BlockKind, BlockDefinition> pair in definitions)
            {
                blockMaterials[pair.Key] = MakeMaterial(pair.Value.Color, 0.02f, 0.58f);
            }
        }

        private void BuildWorld(List<LevelDefinition> levels)
        {
            root = new GameObject("Shapeonaut Rescue V2 World").transform;
            environmentRoot = new GameObject("Diorama Planet").transform;
            environmentRoot.SetParent(root, false);
            buildRoot = new GameObject("Build Blocks").transform;
            buildRoot.SetParent(root, false);
            ghostRoot = new GameObject("Blueprint Ghosts").transform;
            ghostRoot.SetParent(root, false);

            CreateOcean();
            CreatePlanetBase();
            CreateGrid();
            CreateDecor();
            CreateNova();
            CreatePip();
            CreateZones(levels);
        }

        private void CreateOcean()
        {
            GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ocean.name = "Shallow Water";
            ocean.transform.SetParent(environmentRoot, false);
            ocean.transform.position = new Vector3(0f, -0.38f, 0f);
            ocean.transform.localScale = new Vector3(14f, 0.08f, 10f);
            ocean.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Object.Destroy(ocean.GetComponent<Collider>());
        }

        private void CreatePlanetBase()
        {
            for (int x = -5; x <= 5; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    float edge = Mathf.Abs(x) / 5f + Mathf.Abs(z) / 3f;
                    if (edge > 1.35f)
                    {
                        continue;
                    }

                    CreateStaticCube(new Vector3(x * 0.72f, -0.08f, z * 0.72f), new Vector3(0.72f, 0.24f, 0.72f), grassMaterial, "Moss Tile");
                    CreateStaticCube(new Vector3(x * 0.72f, -0.34f, z * 0.72f), new Vector3(0.72f, 0.32f, 0.72f), dirtMaterial, "Soft Rock");
                }
            }
        }

        private void CreateGrid()
        {
            for (int x = ShapeonautBuildModel.GridMinX; x <= ShapeonautBuildModel.GridMaxX; x++)
            {
                CreateGridLine(CellToWorld(new Vector3Int(x, 0, ShapeonautBuildModel.GridMinZ)) + new Vector3(0f, 0.035f, -CellSize * 0.5f), CellToWorld(new Vector3Int(x, 0, ShapeonautBuildModel.GridMaxZ)) + new Vector3(0f, 0.035f, CellSize * 0.5f));
            }

            for (int z = ShapeonautBuildModel.GridMinZ; z <= ShapeonautBuildModel.GridMaxZ; z++)
            {
                CreateGridLine(CellToWorld(new Vector3Int(ShapeonautBuildModel.GridMinX, 0, z)) + new Vector3(-CellSize * 0.5f, 0.036f, 0f), CellToWorld(new Vector3Int(ShapeonautBuildModel.GridMaxX, 0, z)) + new Vector3(CellSize * 0.5f, 0.036f, 0f));
            }
        }

        private void CreateGridLine(Vector3 a, Vector3 b)
        {
            GameObject line = new GameObject("Build Grid Line");
            line.transform.SetParent(environmentRoot, false);
            LineRenderer renderer = line.AddComponent<LineRenderer>();
            renderer.positionCount = 2;
            renderer.SetPosition(0, a);
            renderer.SetPosition(1, b);
            renderer.widthMultiplier = 0.012f;
            renderer.material = gridMaterial;
        }

        private void CreateDecor()
        {
            CreateTree(new Vector3(-4.4f, 0.1f, -2.2f));
            CreateTree(new Vector3(-3.8f, 0.1f, 2.5f));
            CreateTree(new Vector3(4.1f, 0.1f, -2.4f));
            CreateTree(new Vector3(4.6f, 0.1f, 1.8f));
            CreateCrystal(new Vector3(0f, 0.12f, 3.3f));
        }

        private void CreateTree(Vector3 position)
        {
            Transform tree = new GameObject("Soft Pine").transform;
            tree.SetParent(environmentRoot, false);
            tree.position = position;
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree, false);
            trunk.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            trunk.transform.localScale = new Vector3(0.13f, 0.28f, 0.13f);
            trunk.GetComponent<Renderer>().sharedMaterial = dirtMaterial;
            Object.Destroy(trunk.GetComponent<Collider>());

            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.transform.SetParent(tree, false);
            leaves.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            leaves.transform.localScale = new Vector3(0.55f, 0.42f, 0.55f);
            leaves.GetComponent<Renderer>().sharedMaterial = grassMaterial;
            Object.Destroy(leaves.GetComponent<Collider>());
        }

        private void CreateCrystal(Vector3 position)
        {
            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crystal.name = "Planet Core Crystal";
            crystal.transform.SetParent(environmentRoot, false);
            crystal.transform.position = position;
            crystal.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
            crystal.GetComponent<Renderer>().sharedMaterial = MakeTransparentMaterial(new Color(0.8f, 0.95f, 1f, 0.72f), 0f, 0.85f);
            Object.Destroy(crystal.GetComponent<Collider>());
        }

        private void CreateZones(List<LevelDefinition> levels)
        {
            Vector3[] positions =
            {
                new Vector3(-2.8f, 0.13f, -1.6f),
                new Vector3(-1.5f, 0.13f, -1.6f),
                new Vector3(-0.2f, 0.13f, -1.6f),
                new Vector3(1.1f, 0.13f, -1.6f),
                new Vector3(2.4f, 0.13f, -1.6f),
                new Vector3(-2.4f, 0.13f, 1.35f),
                new Vector3(-1.0f, 0.13f, 1.35f),
                new Vector3(0.4f, 0.13f, 1.35f),
                new Vector3(1.8f, 0.13f, 1.35f),
                new Vector3(3.2f, 0.13f, 1.35f)
            };

            for (int i = 0; i < levels.Count && i < positions.Length; i++)
            {
                Transform zone = new GameObject("Build Zone " + levels[i].Id).transform;
                zone.SetParent(environmentRoot, false);
                zone.position = positions[i];
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.transform.SetParent(zone, false);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
                marker.GetComponent<Renderer>().sharedMaterial = MakeTransparentMaterial(new Color(0.32f, 0.65f, 0.95f, 0.75f), 0f, 0.3f);
                Object.Destroy(marker.GetComponent<Collider>());
                zoneMarkers.Add(zone);
            }
        }

        private void CreateNova()
        {
            novaRoot = new GameObject("Nova Shapeonaut").transform;
            novaRoot.SetParent(root, false);
            novaRoot.position = new Vector3(-3.4f, 0.18f, -2.6f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(novaRoot, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(0.38f, 0.52f, 0.38f);
            body.GetComponent<Renderer>().sharedMaterial = whiteMaterial;
            Object.Destroy(body.GetComponent<Collider>());

            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.transform.SetParent(novaRoot, false);
            visor.transform.localPosition = new Vector3(0f, 0.72f, 0.22f);
            visor.transform.localScale = new Vector3(0.32f, 0.16f, 0.035f);
            visor.GetComponent<Renderer>().sharedMaterial = darkMaterial;
            Object.Destroy(visor.GetComponent<Collider>());
        }

        private void CreatePip()
        {
            pipRoot = new GameObject("Pip Companion").transform;
            pipRoot.SetParent(root, false);
            pipRoot.position = novaRoot.position + new Vector3(-0.55f, 0.35f, -0.38f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(pipRoot, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * 0.28f;
            body.GetComponent<Renderer>().sharedMaterial = whiteMaterial;
            Object.Destroy(body.GetComponent<Collider>());

            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.transform.SetParent(pipRoot, false);
            face.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            face.transform.localScale = new Vector3(0.18f, 0.08f, 0.025f);
            face.GetComponent<Renderer>().sharedMaterial = darkMaterial;
            Object.Destroy(face.GetComponent<Collider>());
        }

        private GameObject CreateBlockObject(BlockKind kind, Vector3Int cell, int rotation, Transform parent, Material material, bool interactive, string id)
        {
            Transform blockRoot = new GameObject(kind.ToString()).transform;
            blockRoot.SetParent(parent, false);
            blockRoot.position = CellToWorld(cell);
            blockRoot.rotation = Quaternion.Euler(0f, ShapeonautUtil.NormalizeRotation(rotation) * 90f, 0f);

            if (kind == BlockKind.Ramp)
            {
                CreateRamp(blockRoot, definitions[kind].Size, material, interactive, id);
            }
            else if (kind == BlockKind.TriangularPrism)
            {
                CreateTriPrism(blockRoot, definitions[kind].Size, material, interactive, id);
            }
            else if (kind == BlockKind.Cylinder)
            {
                CreateCylinder(blockRoot, definitions[kind].Size, material, interactive, id);
            }
            else
            {
                CreateCuboid(blockRoot, definitions[kind].Size, material, interactive, id);
            }

            return blockRoot.gameObject;
        }

        private void CreateCuboid(Transform parent, Vector3 size, Material material, bool interactive, string id)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = new Vector3(0f, size.y * LayerHeight * 0.5f, 0f);
            cube.transform.localScale = new Vector3(size.x * CellSize, Mathf.Max(0.08f, size.y * LayerHeight), size.z * CellSize);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (interactive)
            {
                cube.AddComponent<ShapeonautBlockHandle>().Id = id;
            }
            else
            {
                Object.Destroy(cube.GetComponent<Collider>());
            }
        }

        private void CreateCylinder(Transform parent, Vector3 size, Material material, bool interactive, string id)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = new Vector3(0f, size.y * LayerHeight * 0.5f, 0f);
            cylinder.transform.localScale = new Vector3(size.x * CellSize * 0.5f, size.y * LayerHeight * 0.5f, size.z * CellSize * 0.5f);
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            if (interactive)
            {
                cylinder.AddComponent<ShapeonautBlockHandle>().Id = id;
            }
            else
            {
                Object.Destroy(cylinder.GetComponent<Collider>());
            }
        }

        private void CreateRamp(Transform parent, Vector3 size, Material material, bool interactive, string id)
        {
            Mesh mesh = new Mesh();
            float sx = size.x * CellSize * 0.5f;
            float sy = size.y * LayerHeight;
            float sz = size.z * CellSize * 0.5f;
            Vector3[] v =
            {
                new Vector3(-sx, 0f, -sz), new Vector3(sx, 0f, -sz), new Vector3(-sx, 0f, sz), new Vector3(sx, 0f, sz),
                new Vector3(-sx, sy, sz), new Vector3(sx, sy, sz)
            };
            int[] t =
            {
                0, 2, 1, 1, 2, 3,
                2, 4, 3, 3, 4, 5,
                0, 1, 4, 1, 5, 4,
                0, 4, 2,
                1, 3, 5
            };
            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            CreateMeshBlock(parent, mesh, material, interactive, id);
        }

        private void CreateTriPrism(Transform parent, Vector3 size, Material material, bool interactive, string id)
        {
            Mesh mesh = new Mesh();
            float sx = size.x * CellSize * 0.5f;
            float sy = size.y * LayerHeight;
            float sz = size.z * CellSize * 0.5f;
            Vector3[] v =
            {
                new Vector3(-sx, 0f, -sz), new Vector3(sx, 0f, -sz), new Vector3(0f, sy, -sz),
                new Vector3(-sx, 0f, sz), new Vector3(sx, 0f, sz), new Vector3(0f, sy, sz)
            };
            int[] t =
            {
                0, 2, 1, 3, 4, 5,
                0, 3, 2, 2, 3, 5,
                1, 2, 4, 2, 5, 4,
                0, 1, 3, 1, 4, 3
            };
            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            CreateMeshBlock(parent, mesh, material, interactive, id);
        }

        private void CreateMeshBlock(Transform parent, Mesh mesh, Material material, bool interactive, string id)
        {
            GameObject obj = new GameObject("Mesh Block");
            obj.transform.SetParent(parent, false);
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (interactive)
            {
                MeshCollider collider = obj.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                obj.AddComponent<ShapeonautBlockHandle>().Id = id;
            }
        }

        private void CreateStaticCube(Vector3 position, Vector3 scale, Material material, string name)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(environmentRoot, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(cube.GetComponent<Collider>());
        }

        private bool HasMatchingBlock(TargetBlock target, List<PlacedBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                PlacedBlock block = blocks[i];
                if (block.Kind == target.Kind && block.Cell == target.Cell)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleOrbitInput(bool buildMode)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraDistance = Mathf.Clamp(cameraDistance - scroll * 0.55f, 6.2f, 14f);
            }

            if (Input.GetMouseButtonDown(1))
            {
                orbiting = true;
                orbitMouse = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(1))
            {
                orbiting = false;
            }

            if (orbiting)
            {
                Vector3 delta = Input.mousePosition - orbitMouse;
                orbitMouse = Input.mousePosition;
                cameraYaw += delta.x * 0.18f;
                cameraPitch = Mathf.Clamp(cameraPitch - delta.y * 0.14f, buildMode ? 25f : 32f, 78f);
            }
        }

        private void ApplyCamera()
        {
            if (camera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 direction = rotation * Vector3.back;
            camera.transform.position = cameraTarget + direction * cameraDistance;
            camera.transform.LookAt(cameraTarget);
        }

        private Material MakeMaterial(Color color, float metallic, float smoothness)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private Material MakeTransparentMaterial(Color color, float metallic, float smoothness)
        {
            Material material = MakeMaterial(color, metallic, smoothness);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        private void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
