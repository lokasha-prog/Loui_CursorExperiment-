# Installing the Android app (APK) for testing

The file is `dist/BrightFutures-preview.apk` (built from `nms/mobile` with `gradlew assembleRelease`; signed with the debug key, so it is for testing and pilots, not the Play Store).

## Install on a phone
1. Copy the APK to the phone (USB, e-mail to yourself, or a shared folder) and open it. Allow "install unknown apps" for the app you opened it from.
2. Open **Bright Futures**.

## Point it at your server
The app has a **Nursery server address** box on the sign-in screen. Type the address of the computer that runs NMS, for example `http://192.168.1.20:5178`, press **Save address**, then sign in with your own account (the test-user buttons are not in this build).

For the phone to reach the computer:
1. Phone and computer on the same Wi-Fi.
2. Start the API for the network: `powershell -File tools\restart-api.ps1 -Lan`
3. Allow the port once in Windows Firewall (an administrator step): `New-NetFirewallRule -DisplayName "NMS API" -Direction Inbound -Protocol TCP -LocalPort 5178 -Action Allow`
4. Find the computer's address with `ipconfig` (IPv4 Address).

Plain `http` works in this test build on purpose (`usesCleartextTraffic`). The store build uses `https` only.

## Rebuild
```powershell
$env:JAVA_HOME="C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot"; $env:ANDROID_HOME="F:\DEV\android-sdk"
cd nms\mobile; npx expo prebuild --platform android --no-install
cd android; .\gradlew.bat assembleRelease      # output: app\build\outputs\apk\release\app-release.apk
```
