#if !COMPILER_UDONSHARP && UNITY_EDITOR && UDONSHARP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;


namespace MMMaellon
{
    [InitializeOnLoad]
    public class ExampleSceneExporter
    {
        [MenuItem("MMMaellon/" + "~~EXPORT EXAMPLE SCENE~~")]
        static void Export()
        {
            string sourceDir = "Assets/MMMaellon/ExampleScene/";
            // string tempDir = "Assets/MMMaellon/Temp/";
            string exampleDir = "Packages/com.mmmaellon.lightsync/Samples~/Example/";
            // Save the current scene
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            string prevScene = EditorSceneManager.GetActiveScene().path;
            Copy(sourceDir, exampleDir);
            // P_ShootersMenu.Copy(sourceDir, tempDir);
            // // Refresh the AssetDatabase after the copy
            // AssetDatabase.Refresh();

            // string scenePath = tempDir + "ExampleScene.unity";
            // EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // // foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
            // // {
            // //     if (PrefabUtility.IsOutermostPrefabInstanceRoot(obj))
            // //     {
            // //         PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // //     }
            // // }
            // // Save the current scene
            // EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            // P_ShootersMenu.Copy(tempDir, exampleDir);
            // // Refresh the AssetDatabase after the copy
            AssetDatabase.Refresh();

            // ClearDirectory(tempDir);
            // DirectoryInfo dir = new DirectoryInfo(tempDir);
            // dir.Delete();
            // Refresh the AssetDatabase after the copy
            // AssetDatabase.Refresh();

            // EditorSceneManager.OpenScene(prevScene, OpenSceneMode.Single);
        }
        public static void ClearDirectory(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);

            foreach (FileInfo file in dir.GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                ClearDirectory(subDir.FullName);
                subDir.Delete(true);
            }
        }
        public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            if (!Directory.Exists(target.FullName))
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }
}
#endif
