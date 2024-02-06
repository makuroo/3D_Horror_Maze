#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;

namespace FIMSpace.Generating.Rules.Modelling
{
    public class SR_RoofPlaneGenerator : SR_RoofGenerator_TileDes_Base
    {
        public override string TitleName() { return "Roof Generator/Roof Plane Generator"; }

        [Space(4)]
        public Vector2 UVTiling = new Vector2(1, 1);

        [Space(4)]
        public Vector3 Rescale = Vector3.one;
        public Vector3 ExtraOffset = new Vector3(0, 0, 0);
        [Space(2)]
        public Vector2 ExtraSubdivisions = new Vector2(0, 0);


        #region Editor GUI

#if UNITY_EDITOR


        public override void NodeBody(SerializedObject so)
        {
            EditorGUILayout.HelpBox("Generating procedural mesh for the roof side.", MessageType.None);
            base.NodeBody(so);
        }

#endif

        #endregion

        protected override void PrepareMesh(FieldSetup preset, ref SpawnData thisSpawn, FieldCell cell)
        {
            float roofHeight = RoofHeightVariable.GetValue(0f) == 0f ? RoofHeight : RoofHeightVariable.GetValue(0f);
            float roofCenterShift = RoofCenterVariable.GetValue(0f) == 0f ? RoofCenterOffset : RoofCenterVariable.GetValue(0f);

            Quaternion sideRot = Quaternion.LookRotation( Vector3.back);
            Bounds bounds = indicatorsBounds;

            TileDesign des = DoPrepareTileDesign(preset, roofHeight, roofCenterShift);
            des.FullGenerateStack();

            Quaternion extraOffRot = Quaternion.identity;
            if (Rotate90) extraOffRot = Quaternion.Euler(0f, 90f, 0f);

            GameObject tilePrefab = des.GeneratePrefab();
            Vector3 sideLPos = bounds.center;
            tilePrefab.transform.position = sideLPos + sideRot * (ExtraOffset + structureOffset);
            tilePrefab.transform.rotation = extraOffRot * sideRot;

            thisSpawn.SetGeneratorSpaceCoords(tilePrefab.transform.position, tilePrefab.transform.rotation);

            if (roofCenterShift == 0f) // Just symmetrical rotated instance of the generated plane mesh
            {
                GameObject secondSide = Instantiate(tilePrefab);
                Vector3 sideRPos = bounds.center;
                sideRot = Quaternion.LookRotation(Vector3.forward);
                secondSide.transform.position = sideRPos + sideRot * (ExtraOffset + structureOffset);
                secondSide.transform.rotation = extraOffRot * sideRot;
                secondSide.transform.SetParent(tilePrefab.transform, true);
            }
            else
            {
                // Generate second roof side using different roof top offset
                TileDesign oDes = DoPrepareTileDesign(preset, roofHeight, -roofCenterShift);
                oDes.FullGenerateStack();

                GameObject secondSide = oDes.GeneratePrefab();
                Vector3 sideRPos = bounds.center;
                sideRot = Quaternion.LookRotation(Vector3.forward);
                secondSide.transform.position = sideRPos + sideRot * (ExtraOffset + structureOffset);
                secondSide.transform.rotation = sideRot;
                secondSide.transform.SetParent(tilePrefab.transform, true);
            }

            generatedPrefab = tilePrefab;
            tilePrefab.transform.position = new Vector3(10000, 10000, 10000);
            tilePrefab.hideFlags = HideFlags.HideAndDontSave;
            tilePrefab.SetActive(false);
        }


        Vector3 structureOffset;
        TileDesign DoPrepareTileDesign(FieldSetup preset, float roofHeight, float roofCenterShift)
        {
            TileDesign des = new TileDesign();
            TileMeshSetup tileMesh = null;

            Bounds bounds = indicatorsBounds;
            if ( Rotate90) bounds = FEngineering.RotateLocalBounds(bounds, Quaternion.Euler(0, 90, 0));

            structureOffset = Vector3.zero;
            Vector3 cellSize = preset.GetCellUnitSize();

            #region Prepare Tile Design for One Side Roof Shape

            TileDesignPreset tilePres = OptionalSetup;
            if (tilePres != null)
            {
                des.PasteEverythingFrom(tilePres.BaseDesign);
                if (des.TileMeshes.Count > 0) tileMesh = des.TileMeshes[0];
            }

            if (tileMesh == null) tileMesh = new TileMeshSetup("Roof Side");
            des.DesignName = "Roof Side";

            if (TargetMaterial) des.DefaultMaterial = TargetMaterial;
            des.TileMeshes.Add(tileMesh);

            tileMesh.GenTechnique = TileMeshSetup.EMeshGenerator.Loft;
            tileMesh.Origin = EOrigin.BottomCenterBack;

            List<TileMeshSetup.CurvePoint> curve = tileMesh.GetCurve1();

            Color firstColor = Color.white;
            if (curve.Count > 0) firstColor = curve[0].VertexColor;
            if (curve.Count < 2)
            {
                curve.Clear();
                curve.Add(new TileMeshSetup.CurvePoint(new Vector2(0, 0f), true));
                curve.Add(new TileMeshSetup.CurvePoint(new Vector2(0.25f, 0.25f), true));
                curve.Add(new TileMeshSetup.CurvePoint(new Vector2(0.75f, 0.75f), true));
                curve.Add(new TileMeshSetup.CurvePoint(new Vector2(1f, 1f), true));
                for (int c = 0; c < curve.Count; c++) curve[c].VertexColor = firstColor;
            }

            List<TileMeshSetup.CurvePoint> curve2 = tileMesh.GetCurve2();
            if (curve2.Count > 0) firstColor = curve2[0].VertexColor;
            if (curve2.Count < 2)
            {
                curve2.Clear();
                curve2.Add(new TileMeshSetup.CurvePoint(new Vector2(0, 0.5f), true));
                curve2.Add(new TileMeshSetup.CurvePoint(new Vector2(0.35f, 0.5f), true));
                curve2.Add(new TileMeshSetup.CurvePoint(new Vector2(0.65f, 0.5f), true));
                curve2.Add(new TileMeshSetup.CurvePoint(new Vector2(1f, 0.5f), true));
                for (int c = 0; c < curve2.Count; c++) curve2[c].VertexColor = firstColor;
            }

            #endregion

            #region Apply Roof Plane Subdivs

            if (ExtraSubdivisions.x > 0)
            {
                for (int e = 0; e < ExtraSubdivisions.x; e++)
                {
                    List<TileMeshSetup.CurvePoint> backup = new List<TileMeshSetup.CurvePoint>();
                    PGGUtils.TransferFromListToList(curve2, backup);
                    int insrs = 0;
                    for (int c = 0; c < backup.Count - 1; c += 1)
                    {
                        TileMeshSetup.CurvePoint newP = new TileMeshSetup.CurvePoint(Vector2.Lerp(backup[c].localPos, backup[c + 1].localPos, 0.5f), true);
                        curve2.Insert(c + 1 + insrs, newP);
                        insrs += 1;
                    }
                }
            }

            if (ExtraSubdivisions.y > 0)
            {
                for (int e = 0; e < ExtraSubdivisions.y; e++)
                {
                    List<TileMeshSetup.CurvePoint> backup = new List<TileMeshSetup.CurvePoint>();
                    PGGUtils.TransferFromListToList(curve, backup);
                    int insrs = 0;
                    for (int c = 0; c < backup.Count - 1; c += 1)
                    {
                        TileMeshSetup.CurvePoint newP = new TileMeshSetup.CurvePoint(Vector2.Lerp(backup[c].localPos, backup[c + 1].localPos, 0.5f), true);
                        curve.Insert(c + 1 + insrs, newP);
                        insrs += 1;
                    }
                }
            }

            #endregion


            // Adjust roof side plane size
            tileMesh.width = Mathf.Abs(bounds.min.x - bounds.max.x);// * Rescale.x;
            tileMesh.height = roofHeight * Rescale.y;

            tileMesh.depth = Mathf.Abs(bounds.min.z - bounds.max.z);// * Rescale.z;
            tileMesh._loft_forceDepth = true;

            // Prepare Roof Shift
            if (roofCenterShift != 0f)
            {
                float preDepth = tileMesh.depth;
                tileMesh.depth = Mathf.LerpUnclamped(tileMesh.depth, tileMesh.depth * 2f, roofCenterShift);
                structureOffset.z = -(tileMesh.depth - preDepth) / 2f;
            }

            tileMesh.width *= Rescale.x;
            tileMesh.depth *= Rescale.z;


            #region Setup UVs

            tileMesh.UVFit = EUVFit.FitXY;
            tileMesh.UVMul = UVTiling;

            if (tileMesh.width > cellSize.x)
            {
                float xRatio = tileMesh.width / cellSize.x;
                tileMesh.UVMul.x *= xRatio;
            }

            if (tileMesh.height > cellSize.y)
            {
                float yRatio = tileMesh.height / cellSize.y;
                tileMesh.UVMul.y *= yRatio;
            }

            if (tileMesh.depth > cellSize.z)
            {
                if (tileMesh.depth > tileMesh.height)
                {
                    float yRatio = tileMesh.height / cellSize.y;
                    float zRatio = (tileMesh.depth / cellSize.z) * 0.25f;
                    float heightDepth = (tileMesh.height) / (tileMesh.depth);
                    //float depthHeight = (tileMesh.depth) / (tileMesh.height);
                    //UnityEngine.Debug.Log("depthHeight = " + (depthHeight / cellSize.z) + " hdepthRaw : " + heightDepth + " yRatio : " + yRatio + " zRatio : " + zRatio);

                    float yzRatio = Mathf.Lerp(zRatio, yRatio, heightDepth);
                    tileMesh.UVMul.y = UVTiling.y * (yzRatio);
                }
            }

            #endregion


            return des;
        }



    }
}