# NDI_AndroidXR

## NDI SDK integration (Unity)

### Where to download
1. Go to the official NDI SDK downloads page: https://ndi.video/tools/ndi-sdk/
2. Download the **NDI 5 SDK for Unity** (or the latest Unity package for NDI Tools/SDK).

### Import into Unity
1. Unzip the downloaded SDK.
2. In Unity, open **Window → Package Manager**.
3. Click **+ → Add package from disk…** and select the SDK `package.json` (or import the provided `.unitypackage` if that is what the SDK contains).
4. Confirm that the NDI plugin appears in **Project → Assets** and that any native libraries for Android are present in the imported package.

### Enable NDI scripting define symbols
* In **Project Settings → Player → Other Settings → Scripting Define Symbols**, add `NDI_SDK_ENABLED` for the target build group (for example, **Android**). The receiver scripts are wrapped in `#if NDI_SDK_ENABLED` and will no-op without it.

### Android native libraries
* From the NewTek NDI SDK for Unity/Android, copy the Android native libraries into your Unity project under `Assets/Plugins/Android/` with ABI subfolders. For example:
  * `Assets/Plugins/Android/arm64-v8a/libndi.so`
  * `Assets/Plugins/Android/arm64-v8a/libndi_sdk.so`
  * Repeat for any additional ABIs you support (e.g., `armeabi-v7a`).
* In the Unity Editor, select each `.so` and ensure the **Plugin Import Settings** target **Android** (and the proper CPU architecture).

### Licensing notes
* NDI is a proprietary SDK. You must review and accept the NDI SDK EULA/license terms on the official download page before using it in a project.
* Redistribution rules vary by NDI version. Ensure your final app distribution complies with the SDK’s licensing requirements.

## AndroidXR/OpenXR setup (Unity + Android)

### Unity project configuration
1. Use a supported Unity version (verify against AndroidXR/OpenXR documentation and your target headset vendor requirements).
2. Install the **OpenXR Plugin** in **Package Manager** (use a version that matches your Android XR device/runtime requirements).
3. In **Project Settings → XR Plug-in Management**, enable **OpenXR** for **Android**.
4. In **Project Settings → OpenXR**, enable the required interaction profiles and features for your target device.
5. The **Android XR Unity package** is an optional external dependency. If you are targeting Android XR runtimes, install it from the official GitHub repo (https://github.com/android/android-xr-unity) and follow its setup guidance (it provides Android XR-specific OpenXR features and settings). In Unity, open **Window → Package Manager → + → Add package from git URL…** and paste the repo URL (or the specific package URL if the repo instructs a subpath).
6. For passthrough, confirm that the OpenXR feature/extension needed by your device (for example **XR_ANDROID_composition_layer_passthrough_mesh** or **XR_FB_passthrough**) is available in your OpenXR plugin version and enabled in **Project Settings → OpenXR**.

### XR Interaction Toolkit alignment
* Unity’s XR Interaction Toolkit 3.x introduces new interaction patterns and improved OpenXR support compared to 2.x. If you plan to use the 3.4 documentation, upgrade this project to 3.4 first, update `UnityProject/Packages/manifest.json`, and verify any API changes in interaction scripts.

### AndroidXR tooling
1. Install **Android Studio** and the **Android SDK/NDK** versions required by your Unity version.
2. In **Unity → Preferences → External Tools**, point Unity to the Android SDK/NDK/JDK paths.
3. Enable **ARM64** (and other required ABIs) in **Project Settings → Player → Other Settings**.

### Samsung Galaxy XR build requirements
* Use the headset vendor’s recommended Unity version, Android API level, and OpenXR feature set.
* Ensure **ARM64** is enabled and that any vendor-specific OpenXR extensions are turned on.
* If the headset requires a **developer mode** or **device registration**, complete those steps before deployment.
* Verify your headset OS build and firmware version meet the minimum required by the vendor’s OpenXR runtime.

## Build & deployment (APK generation + installation)

### Generate an APK in Unity
1. Open **File → Build Settings**.
2. Select **Android** and click **Switch Platform** (if needed).
3. Ensure **XR Plug-in Management → OpenXR** is enabled for Android.
4. In **Player Settings → Publishing Settings**, configure your keystore and signing credentials.
5. Click **Build** to generate the APK.

### Install to headset
1. Enable **Developer Options** and **USB debugging** on the headset.
2. Connect the headset via USB and verify it appears in `adb devices`.
3. Install the APK:
   ```bash
   adb install -r path/to/YourApp.apk
   ```
4. Launch the app from the headset’s app launcher.

## Troubleshooting

### Build issues
* **Gradle/SDK errors**: Confirm Unity’s Android SDK/NDK/JDK paths are correct in **Preferences → External Tools** and that the versions match Unity’s requirements.
* **OpenXR plugin errors**: Ensure the OpenXR package is installed and enabled for Android in **XR Plug-in Management**.
* **ABI mismatch**: Confirm **ARM64** is enabled and that your NDI native libs include matching Android binaries.

### Runtime issues
* **Black screen or app immediately closes**: Check device logs with `adb logcat` to identify missing permissions, missing native libs, or OpenXR runtime issues.
* **NDI stream not visible**: Verify network connectivity, NDI sender availability, and that the device is on the same subnet as the sender.
* **Controller input not working**: Confirm the correct OpenXR interaction profiles are enabled and that the runtime is active on the headset.
