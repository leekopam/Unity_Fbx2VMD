1. This project is developed based on operation within the build (runtime) environment and must function identically within the editor environment. However, certain features for testing purposes only work within the editor environment.

2. Pipeline for the Unity project under development
Implement file selection functionality using the UnityStandaloneFileBrowser library
-> Select and import binary-format FBX files generated via motion capture using the file selection feature; data is imported using the assimp-net library
-> In the runtime environment, uncheck (set to false) the Strip Bones option in the Rig tab of the imported FBX file and set the Animation type to Humanoid.
-> Map the bones in the FBX file using the BoneMapping_Data.txt file located in Assets>Resources
-> In the Hierarchy window, assign the animation clip file from the imported FBX file to the testPrefab prefab. Assign the rounded-up Length value of the animation clip file to the StopRecordingTime variable in the HumanoidVMDRecorder.cs script attached to the testPrefab prefab.
-> The testPrefab animation playback timing starts when all preprocessing tasks for generating the VMD file are completed in the UnityVMDRecorder library. It then begins animation playback while initiating VMD recording via the UnityVMDRecorder library.

3. Reference Materials for Project Development
- “2. Pipeline for the Unity Project Under Development” was configured based on the contents of the FBX to VMD_Unity.docx file located in the Assets folder.
- Runtime-imported FBX files are associated with the BoneMapping_Data.txt and FBX_BoneMapping_Data.ht files located in the Assets>Resources path.

- The prefab used for animation playback is always the testPrefab prefab in the Hierarchy window. The bone file for this prefab is testPrefab_Bone.ht located in Assets>Resources.
- The testPrefab prefab has its Animation Type set to Humanoid.
- The testPrefab prefab uses an Animator Controller named TestAnimator1, which contains only one State: the Default State named satisfaction_2_FBX.
- The sole State of the testPrefab prefab, satisfaction_2_FBX, has an empty dummy Animation Clip named EmptyClip assigned to its Motion area with no settings configured.

4. Attempted Solutions
- After importing the FBX file into the runtime environment, directly assigning the animation clip file contained within the FBX file to the Motion section of satisfaction_2_FBX allowed the animation to play without issues.