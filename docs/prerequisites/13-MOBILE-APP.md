# 13 — Mobile App (React Native + Expo)

> React Native 0.74 + Expo SDK 51 (Bare Workflow). iOS + Android native apps for staff and parents. Offline-first where critical.

---

## 1. Mobile Scope

### 1.1 Two Apps, One Codebase

NMS mobile is delivered as **two apps** from a single React Native codebase:

| App | Target users | Phase | Key features |
|---|---|---|---|
| **NMS Staff** | Teachers, Centre Admins, Receptionists | Phase 1 Sprint 15–16 | Dashboard, attendance (barcode scan), activities, messaging, emergency cache |
| **NMS Parents** | Parents/Guardians | Phase 1 Sprint 15–16 | Activity feed, daily report, messaging, notifications |
| **NMS Learning** (Phase 2+) | Teachers (observations, assessments) | Phase 2 | Adds observation capture, learning journey view |

Both apps share ~70% of code (auth, API client, i18n, state, design system). Different entry points + role-aware navigation.

### 1.2 Why React Native + Expo (Bare Workflow)

- **Cross-platform**: iOS + Android from one codebase.
- **JS ecosystem**: shares TypeScript types, i18n strings, API client, Zod schemas with web.
- **Expo tooling**: EAS Build (cloud builds), OTA updates, config plugins, Expo Go for dev.
- **Bare workflow**: allows native modules where needed (barcode, SQLite, push notifications, biometrics).
- **AI-coding-friendly**: React Native is well-documented in AI training data.

### 1.3 Why Not Expo Managed Workflow

Managed workflow doesn't support:
- Custom native modules (we need `react-native-vision-camera` for barcode, `watermelonDB`/`react-native-sqlite-storage` for offline, `react-native-keychain` for secure storage).
- Custom native build configurations (Stripe SDK, background tasks).

Bare workflow gives us Expo's productivity + native flexibility.

---

## 2. Stack

| Layer | Technology | Why |
|---|---|---|
| Framework | React Native 0.74 | Cross-platform, mature |
| Toolchain | Expo SDK 51 (Bare) | EAS Build, OTA updates, config plugins |
| Language | TypeScript 5.5 | Same as web |
| Navigation | Expo Router v3 | File-based routing, deep linking, type-safe |
| Styling | NativeWind (Tailwind for RN) | Same utility classes as web |
| UI primitives | React Native Paper | Accessible, Material Design-based |
| Icons | @expo/vector-icons (Lucide) | Same icon set as web |
| State (server) | TanStack Query v5 | Same as web |
| State (client) | Zustand | Same as web |
| Forms | react-hook-form + zod | Same as web |
| i18n | i18next + react-i18next | Same as web (shared translation files) |
| Auth | expo-auth-session + Keycloak | PKCE flow |
| Secure storage | expo-secure-store | iOS Keychain + Android EncryptedSharedPreferences |
| Push notifications | expo-notifications + FCM (Android) + APNs (iOS) | Unified API |
| Camera | react-native-vision-camera | Barcode scanning, photo capture |
| Barcode | MLKit (via vision-camera) | On-device, fast, no network |
| Offline DB | WatermelonDB (built on SQLite) | Sync engine, observable queries |
| SignalR | @microsoft/signalr | Same client as web |
| HTTP | axios | Same as web |
| Dates | date-fns + date-fns-tz | Same as web |
| Testing | Jest + React Native Testing Library + Detox (E2E) | RN standard |
| Crash reporting | Sentry React Native | Errors + performance |
| Analytics | PostHog (or Amplitude) | Product analytics |

---

## 3. Project Structure

```
mobile/
├── app/                          # Expo Router file-based routing
│   ├── _layout.tsx               # Root layout (providers, auth gate)
│   ├── (auth)/                   # Auth group (no auth required)
│   │   ├── login.tsx
│   │   ├── forgot-password.tsx
│   │   ├── reset-password.tsx
│   │   └── accept-invitation.tsx
│   ├── (staff)/                  # Staff group (auth + staff role)
│   │   ├── _layout.tsx           # Tab navigator
│   │   ├── dashboard.tsx
│   │   ├── attendance/
│   │   │   ├── index.tsx         # List view
│   │   │   └── scan.tsx          # Camera scanner
│   │   ├── activities/
│   │   │   ├── index.tsx
│   │   │   └── new.tsx
│   │   ├── messaging/
│   │   │   ├── index.tsx
│   │   │   └── [threadId].tsx
│   │   ├── emergencies/
│   │   │   └── index.tsx         # Cached emergency reports
│   │   └── profile.tsx
│   ├── (parent)/                 # Parent group (auth + parent role)
│   │   ├── _layout.tsx
│   │   ├── feed.tsx
│   │   ├── daily-report/
│   │   │   └── [date].tsx
│   │   ├── messaging/
│   │   │   ├── index.tsx
│   │   │   └── [threadId].tsx
│   │   ├── invoices/             # Phase 3
│   │   │   ├── index.tsx
│   │   │   └── [invoiceId].tsx
│   │   └── profile.tsx
│   └── +not-found.tsx
├── src/
│   ├── api/                      # Shared with web (OpenAPI-generated)
│   ├── components/
│   │   ├── ui/                   # RN equivalents of shadcn/ui
│   │   │   ├── Button.tsx
│   │   │   ├── Input.tsx
│   │   │   ├── Card.tsx
│   │   │   ├── Badge.tsx
│   │   │   └── ...
│   │   └── shared/
│   │       ├── EntityList.tsx
│   │       ├── EmptyState.tsx
│   │       ├── LoadingState.tsx
│   │       ├── ErrorState.tsx
│   │       └── OfflineBanner.tsx
│   ├── hooks/
│   │   ├── useAuth.ts
│   │   ├── useSignalR.ts
│   │   ├── useOfflineSync.ts
│   │   ├── usePushNotifications.ts
│   │   └── ...
│   ├── stores/
│   │   ├── authStore.ts          # Same as web
│   │   ├── uiStore.ts
│   │   └── offlineQueueStore.ts
│   ├── lib/
│   │   ├── i18n.ts               # Same as web (shared translations)
│   │   ├── api-client.ts
│   │   ├── auth.ts               # Keycloak RN adapter
│   │   ├── signalr.ts
│   │   ├── offline-db.ts         # WatermelonDB setup
│   │   ├── push-notifications.ts
│   │   └── utils.ts
│   ├── model/                    # WatermelonDB schemas
│   │   ├── schema.ts
│   │   ├── Student.ts
│   │   ├── EmergencyReport.ts
│   │   └── ...
│   └── types/
├── assets/
│   ├── images/
│   └── fonts/
├── app.config.ts                 # Dynamic Expo config (per env)
├── package.json
├── tsconfig.json
├── tailwind.config.ts            # NativeWind
├── eas.json                      # EAS Build config
└── App.tsx                       # Entry point (only for bare workflow)
```

---

## 4. Authentication

### 4.1 Keycloak Login (PKCE Flow)

```tsx
// lib/auth.ts
import * as AuthSession from 'expo-auth-session';
import * as SecureStore from 'expo-secure-store';
import * as WebBrowser from 'expo-web-browser';

const discovery = {
  authorizationEndpoint: `${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/auth`,
  tokenEndpoint: `${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token`,
  revocationEndpoint: `${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/revoke`,
};

const redirectUri = AuthSession.makeRedirectUri({
  scheme: 'nms',
  path: 'auth/callback',
});

const config = {
  clientId: 'nms-mobile',
  clientSecret: null,  // PKCE, no secret
  redirectUri,
  scopes: ['openid', 'profile', 'email'],
};

export async function login() {
  const request = new AuthSession.AuthRequest(config);
  const result = await request.promptAsync(discovery);
  
  if (result.type === 'success') {
    const code = result.params.code;
    const tokens = await exchangeCodeForTokens(code, request.codeVerifier);
    
    await SecureStore.setItemAsync('access_token', tokens.accessToken);
    await SecureStore.setItemAsync('refresh_token', tokens.refreshToken);
    
    return tokens;
  }
  
  throw new Error('Login failed');
}

export async function getAccessToken(): Promise<string | null> {
  return SecureStore.getItemAsync('access_token');
}

export async function refreshTokens(): Promise<string | null> {
  const refreshToken = await SecureStore.getItemAsync('refresh_token');
  if (!refreshToken) return null;
  
  try {
    const response = await fetch(`${discovery.tokenEndpoint}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'refresh_token',
        client_id: config.clientId,
        refresh_token: refreshToken,
      }),
    });
    
    const tokens = await response.json();
    
    await SecureStore.setItemAsync('access_token', tokens.access_token);
    await SecureStore.setItemAsync('refresh_token', tokens.refresh_token);
    
    return tokens.access_token;
  } catch (error) {
    await logout();
    return null;
  }
}

export async function logout() {
  await SecureStore.deleteItemAsync('access_token');
  await SecureStore.deleteItemAsync('refresh_token');
}
```

### 4.2 Biometric Unlock (Optional)

After first login, user can enable biometric unlock (Face ID / Touch ID / fingerprint):

```tsx
import * as LocalAuthentication from 'expo-local-authentication';

export async function unlockWithBiometrics(): Promise<boolean> {
  const hasHardware = await LocalAuthentication.hasHardwareAsync();
  if (!hasHardware) return false;
  
  const isEnrolled = await LocalAuthentication.isEnrolledAsync();
  if (!isEnrolled) return false;
  
  const result = await LocalAuthentication.authenticateAsync({
    promptMessage: t('auth.biometricPrompt'),
    fallbackLabel: t('auth.usePassword'),
  });
  
  return result.success;
}
```

App stores refresh token in SecureStore, on launch:
1. Check if biometric enabled.
2. If yes, prompt biometric.
3. On success, use stored refresh token to get new access token.
4. On failure, fall back to password login.

---

## 5. Offline Architecture

### 5.1 Offline Strategy

| Feature | Offline capability | Storage |
|---|---|---|
| Emergency Child Report | Full offline, refreshed daily | WatermelonDB (SQLite) |
| Attendance check-in/out | Limited offline, sync when online | WatermelonDB + queue |
| Activity feed viewing | Cached last 7 days | WatermelonDB |
| Activity post creation | Queue + upload when online | WatermelonDB + queue |
| Messaging | Read cached last 50 per thread | WatermelonDB |
| Messaging send | Queue + send when online | WatermelonDB + queue |
| Learning Journey | Cached last 30 days | WatermelonDB |
| Dashboard | No offline — show "offline" banner | N/A |

### 5.2 WatermelonDB Setup

```tsx
// lib/offline-db.ts
import { Database } from '@nozbe/watermelondb';
import SQLiteAdapter from '@nozbe/watermelondb/adapters/sqlite';
import { schema, migrations, modelClasses } from '../model';

const adapter = new SQLiteAdapter({
  schema,
  migrations,
  jsi: true,  // Faster JSI implementation (RN 0.74+)
  dbName: 'nms-offline',
});

export const database = new Database({
  adapter,
  modelClasses,
});

// model/schema.ts
import { appSchema, tableSchema } from '@nozbe/watermelondb';

export const schema = appSchema({
  version: 1,
  tables: [
    tableSchema({
      name: 'students',
      columns: [
        { name: 'server_id', type: 'string', isIndexed: true },
        { name: 'first_name', type: 'string' },
        { name: 'last_name', type: 'string' },
        { name: 'date_of_birth', type: 'string' },
        { name: 'room_id', type: 'string', isIndexed: true },
        { name: 'photo_url', type: 'string' },
        { name: 'medical_notes_encrypted', type: 'string' },
        { name: 'allergy_info_encrypted', type: 'string' },
        { name: 'emergency_contacts_json', type: 'string' },
        { name: 'cached_at', type: 'number' },
      ],
    }),
    tableSchema({
      name: 'emergency_reports',
      columns: [
        { name: 'student_id', type: 'string', isIndexed: true },
        { name: 'pdf_path', type: 'string' },
        { name: 'generated_at', type: 'number' },
      ],
    }),
    tableSchema({
      name: 'pending_check_ins',
      columns: [
        { name: 'student_id', type: 'string' },
        { name: 'check_in_at', type: 'number' },
        { name: 'method', type: 'string' },
        { name: 'sync_status', type: 'string' },  // 'pending', 'synced', 'failed'
        { name: 'sync_error', type: 'string' },
      ],
    }),
    tableSchema({
      name: 'pending_activity_posts',
      columns: [
        { name: 'type', type: 'string' },
        { name: 'caption_json', type: 'string' },
        { name: 'media_paths', type: 'string' },  // Local file paths
        { name: 'student_ids', type: 'string' },
        { name: 'created_at', type: 'number' },
        { name: 'sync_status', type: 'string' },
      ],
    }),
    tableSchema({
      name: 'cached_activities',
      columns: [
        { name: 'server_id', type: 'string', isIndexed: true },
        { name: 'student_id', type: 'string', isIndexed: true },
        { name: 'type', type: 'string' },
        { name: 'caption_json', type: 'string' },
        { name: 'thumbnail_url', type: 'string' },
        { name: 'posted_at', type: 'number' },
      ],
    }),
    // ... more tables
  ],
});
```

### 5.3 Sync Engine

```tsx
// lib/offline-sync.ts
import { database } from './offline-db';
import { API } from './api-client';
import NetInfo from '@react-native-community/netinfo';

export class SyncEngine {
  private isSyncing = false;
  
  constructor() {
    // Listen for network state changes
    NetInfo.addEventListener(state => {
      if (state.isConnected && !this.isSyncing) {
        this.syncAll();
      }
    });
  }
  
  async syncAll() {
    if (this.isSyncing) return;
    this.isSyncing = true;
    
    try {
      await this.syncPendingCheckIns();
      await this.syncPendingActivityPosts();
      await this.refreshEmergencyReports();
      await this.refreshCachedActivities();
    } finally {
      this.isSyncing = false;
    }
  }
  
  private async syncPendingCheckIns() {
    const pending = await database.collections.get('pending_check_ins')
      .query(Q.where('sync_status', 'pending')).fetch();
    
    for (const record of pending) {
      try {
        await API.post('/attendance/actions/check-in', {
          studentId: record.studentId,
          checkInAt: new Date(record.checkInAt).toISOString(),
          method: record.method,
        });
        
        await database.write(async () => {
          await record.update(r => {
            r.syncStatus = 'synced';
          });
        });
      } catch (error) {
        await database.write(async () => {
          await record.update(r => {
            r.syncStatus = 'failed';
            r.syncError = error.message;
          });
        });
      }
    }
  }
  
  // ... other sync methods
}

export const syncEngine = new SyncEngine();
```

### 5.4 Conflict Resolution

Server timestamp wins. If local check-in timestamp differs from server, server's timestamp is authoritative. Conflicts logged for audit.

For activity posts: if post was created offline and parent approval was auto-granted on server, the synced post reflects server-side approval_status.

---

## 6. Push Notifications

### 6.1 Setup

```tsx
// lib/push-notifications.ts
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
    priority: Notifications.AndroidNotificationPriority.HIGH,
  }),
});

export async function registerForPushNotifications(): Promise<string | null> {
  const { status: existingStatus } = await Notifications.getPermissionsAsync();
  let finalStatus = existingStatus;
  
  if (existingStatus !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync();
    finalStatus = status;
  }
  
  if (finalStatus !== 'granted') {
    return null;
  }
  
  const token = (await Notifications.getExpoPushTokenAsync({
    projectId: EXPO_PROJECT_ID,
  })).data;
  
  // Register token with backend
  await API.post('/devices/register', {
    token,
    platform: Platform.OS,
    appVersion: AppInfo.version,
  });
  
  return token;
}

// Handle notification tap (deep link)
Notifications.addNotificationResponseReceivedListener(response => {
  const { data } = response.notification.request.content;
  
  if (data.threadId) {
    router.push(`/messaging/${data.threadId}`);
  } else if (data.activityId) {
    router.push(`/feed?activityId=${data.activityId}`);
  } else if (data.notificationType === 'ratio_breach') {
    router.push('/dashboard');
  }
});

// Handle notification received while app is foreground
Notifications.addNotificationReceivedListener(notification => {
  // Update notification badge, show in-app banner
  const { title, body, data } = notification.request.content;
  showInAppBanner({ title, body, data });
});
```

### 6.2 Notification Channels (Android)

```tsx
async function setupAndroidChannels() {
  if (Platform.OS !== 'android') return;
  
  await Notifications.setNotificationChannelAsync('critical', {
    name: 'Critical Alerts',
    importance: Notifications.AndroidImportance.MAX,
    vibrationPattern: [0, 250, 250, 250],
    lightColor: '#FF0000',
    sound: 'critical_alert.wav',
  });
  
  await Notifications.setNotificationChannelAsync('operational', {
    name: 'Operational',
    importance: Notifications.AndroidImportance.HIGH,
    lightColor: '#FFA500',
  });
  
  await Notifications.setNotificationChannelAsync('informational', {
    name: 'Informational',
    importance: Notifications.AndroidImportance.LOW,
  });
}
```

### 6.3 Critical Alert Entitlement (iOS)

For Critical tier notifications to bypass Do Not Disturb on iOS, we need Apple's Critical Alert Entitlement (requires Apple approval + justification):

```tsx
// In app.config.ts
ios: {
  infoPlist: {
    'UIBackgroundModes': ['remote-notification', 'audio'],
  },
  entitlements: {
    'com.apple.developer.usernotifications.critical-alerts': true,
  },
}
```

Apply for entitlement via Apple Developer portal. Justification: childcare safety alerts.

---

## 7. Barcode Scanning

### 7.1 Camera + MLKit

```tsx
// app/(staff)/attendance/scan.tsx
import { Camera, useCameraDevices, useFrameProcessor } from 'react-native-vision-camera';
import { useScanBarcodes, BarcodeFormat } from 'vision-camera-code-scanner';

export default function AttendanceScanScreen() {
  const devices = useCameraDevices();
  const device = devices.back;
  const [hasPermission, setHasPermission] = useState(false);
  const [scannedBarcodes, setScannedBarcodes] = useState<string[]>([]);
  
  const frameProcessor = useFrameProcessor((frame) => {
    'worklet';
    const barcodes = scanBarcodes(frame, [BarcodeFormat.CODE_128, BarcodeFormat.QR_CODE]);
    runOnJS(setScannedBarcodes)(barcodes.map(b => b.rawValue));
  }, []);
  
  useEffect(() => {
    (async () => {
      const status = await Camera.requestCameraPermission();
      setHasPermission(status === 'authorized');
    })();
  }, []);
  
  useEffect(() => {
    if (scannedBarcodes.length > 0) {
      handleBarcodeScan(scannedBarcodes[0]);
    }
  }, [scannedBarcodes]);
  
  if (!hasPermission) return <RequestCameraPermission />;
  if (!device) return <LoadingState />;
  
  return (
    <View className="flex-1">
      <Camera
        device={device}
        isActive={true}
        frameProcessor={frameProcessor}
        style={StyleSheet.absoluteFill}
      />
      <View className="absolute inset-0 flex items-center justify-center">
        <View className="h-48 w-72 border-4 border-primary rounded-lg" />
        <Text className="text-white mt-4">{t('attendance.scanHint')}</Text>
      </View>
    </View>
  );
}

async function handleBarcodeScan(barcode: string) {
  try {
    // Haptic feedback
    await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
    
    // Online: call API directly
    if (await isOnline()) {
      await API.post('/attendance/actions/check-in', { barcode });
      showSuccessOverlay();
    } else {
      // Offline: queue locally
      await queueCheckInOffline(barcode);
      showOfflineQueuedOverlay();
    }
  } catch (error) {
    await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
    showErrorOverlay(error.message);
  }
}
```

### 7.2 USB/Bluetooth HID Scanners (Tablets)

For tablets with USB/Bluetooth HID scanners (keyboard emulation), no camera needed — scanners send keystrokes + Enter:

```tsx
// Hook for HID scanner input
function useHidScanner(onScan: (barcode: string) => void) {
  const [buffer, setBuffer] = useState('');
  
  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key === 'Enter') {
        if (buffer.length > 0) {
          onScan(buffer);
          setBuffer('');
        }
      } else if (event.key.length === 1) {
        setBuffer(prev => prev + event.key);
      }
    };
    
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [buffer, onScan]);
}
```

---

## 8. Emergency Report Offline Cache

This is the **most critical** offline feature. During an evacuation, network may be down. Staff must access emergency reports instantly.

### 8.1 Cache Strategy

```
Daily refresh (3 AM local time, via background task):
  → For each active student in assigned rooms:
    → Fetch emergency report PDF from API
    → Download to local filesystem
    → Update WatermelonDB record with local path
  
On-demand refresh (user taps "Refresh" button):
  → Same as above, immediate

On app launch (if online):
  → Check if last refresh > 24h
  → If yes, refresh in background
```

### 8.2 Implementation

```tsx
// hooks/useEmergencyReports.ts
export function useEmergencyReports() {
  const [reports, setReports] = useState<EmergencyReport[]>([]);
  const [isRefreshing, setIsRefreshing] = useState(false);
  
  useEffect(() => {
    // Load from local DB
    const subscription = database.collections.get('emergency_reports')
      .query()
      .observeWithColumns(['generated_at'])
      .subscribe(setReports);
    
    return () => subscription.unsubscribe();
  }, []);
  
  const refresh = async () => {
    setIsRefreshing(true);
    try {
      const students = await database.collections.get('students')
        .query(Q.where('room_id', Q.notIn([])))
        .fetch();
      
      for (const student of students) {
        const pdfResponse = await API.get(`/students/${student.serverId}/emergency-report`, {
          responseType: 'blob',
        });
        
        const localPath = `${FileSystem.documentDirectory}emergency-reports/${student.serverId}.pdf`;
        await FileSystem.writeAsStringAsync(localPath, pdfResponse.data, {
          encoding: FileSystem.EncodingType.Base64,
        });
        
        await database.write(async () => {
          await database.collections.get('emergency_reports').create(record => {
            record.studentId = student.serverId;
            record.pdfPath = localPath;
            record.generatedAt = Date.now();
          });
        });
      }
    } finally {
      setIsRefreshing(false);
    }
  };
  
  return { reports, refresh, isRefreshing };
}
```

### 8.3 Background Refresh

```tsx
// Use expo-background-fetch for daily refresh
import * as BackgroundFetch from 'expo-background-fetch';
import * as TaskManager from 'expo-task-manager';

const EMERGENCY_REFRESH_TASK = 'EMERGENCY_REFRESH';

TaskManager.defineTask(EMERGENCY_REFRESH_TASK, async () => {
  try {
    await refreshEmergencyReports();
    return BackgroundFetch.Result.NewData;
  } catch (error) {
    return BackgroundFetch.Result.Failed;
  }
});

async function registerBackgroundTask() {
  await BackgroundFetch.registerTaskAsync(EMERGENCY_REFRESH_TASK, {
    minimumInterval: 60 * 60 * 24,  // 24 hours
    stopOnTerminate: false,
    startOnBoot: true,
  });
}
```

### 8.4 Access

```tsx
// app/(staff)/emergencies/index.tsx
import * as FileSystem from 'expo-file-system';
import * as Sharing from 'expo-sharing';

export default function EmergencyReportsScreen() {
  const { reports, refresh, isRefreshing } = useEmergencyReports();
  
  const openReport = async (report: EmergencyReport) => {
    if (await Sharing.isAvailableAsync()) {
      await Sharing.shareAsync(report.pdfPath, {
        mimeType: 'application/pdf',
        dialogTitle: `${report.studentName} - Emergency Report`,
      });
    }
  };
  
  return (
    <Screen>
      <Header 
        title={t('emergencies.title')}
        right={<RefreshButton onPress={refresh} isLoading={isRefreshing} />}
      />
      <FlatList
        data={reports}
        keyExtractor={r => r.id}
        renderItem={({ item }) => (
          <Pressable onPress={() => openReport(item)}>
            <Card>
              <Text>{item.studentName}</Text>
              <Text>{t('emergencies.cachedAt')}: {formatDateTime(item.generatedAt)}</Text>
            </Card>
          </Pressable>
        )}
      />
    </Screen>
  );
}
```

---

## 9. UI Components (NativeWind)

### 9.1 Tailwind for RN

```tsx
// tailwind.config.ts (NativeWind)
module.exports = {
  content: ['./app/**/*.{tsx,ts}', './src/**/*.{tsx,ts}'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        // Same palette as web
        primary: '#0066CC',
        // ...
      },
    },
  },
  plugins: [],
};
```

### 9.2 Component Example

```tsx
// src/components/ui/Button.tsx
import { Pressable, Text } from 'react-native';
import { cva, type VariantProps } from 'class-variance-authority';

const buttonVariants = cva('flex flex-row items-center justify-center rounded-lg px-4 py-2', {
  variants: {
    variant: {
      primary: 'bg-primary text-primary-foreground',
      secondary: 'bg-secondary text-secondary-foreground',
      outline: 'border border-input bg-transparent',
      ghost: 'bg-transparent',
      destructive: 'bg-destructive text-destructive-foreground',
    },
    size: {
      sm: 'h-8 px-3 text-sm',
      md: 'h-10 px-4',
      lg: 'h-12 px-6 text-lg',
    },
  },
  defaultVariants: {
    variant: 'primary',
    size: 'md',
  },
});

interface ButtonProps extends VariantProps<typeof buttonVariants> {
  children: React.ReactNode;
  onPress?: () => void;
  disabled?: boolean;
}

export function Button({ variant, size, children, onPress, disabled }: ButtonProps) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      className={buttonVariants({ variant, size })}
    >
      <Text>{children}</Text>
    </Pressable>
  );
}
```

---

## 10. Performance

### 10.1 List Optimization

- Use `FlatList` (never `ScrollView` for long lists).
- `removeClippedSubviews={true}` for very long lists.
- `maxToRenderPerBatch={10}` and `windowSize={5}` for memory efficiency.
- `getItemLayout` for fixed-height items (avoids measurement).
- Memoize row components with `React.memo`.
- Use `useCallback` for handlers.

### 10.2 Image Optimization

- Use `expo-image` (faster than `Image`, with caching + progressive loading).
- Specify `placeholder` for blur-up effect.
- Set `cachePolicy="memory-disk"`.

```tsx
import { Image } from 'expo-image';

<Image
  source={{ uri: photoUrl }}
  placeholder={{ blurhash: 'LKO2?U%2Tw=w]~RBVZRi};RPxuwH' }}
  contentFit="cover"
  cachePolicy="memory-disk"
  transition={200}
  style={{ width: 100, height: 100 }}
/>
```

### 10.3 Bundle Size

- Hermes engine (enabled by default in Expo SDK 51).
- ProGuard/R8 for Android (enabled in release build).
- Strip unused Reanimated worklets.
- Tree-shake unused i18n locales.

Target: < 50 MB Android APK, < 60 MB iOS IPA.

---

## 11. Build & Release

### 11.1 EAS Build

```json
// eas.json
{
  "cli": {
    "version": ">= 5.0.0"
  },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal",
      "channel": "development",
      "env": {
        "APP_VARIANT": "development"
      }
    },
    "staging": {
      "distribution": "internal",
      "channel": "staging",
      "env": {
        "APP_VARIANT": "staging"
      }
    },
    "production": {
      "distribution": "store",
      "channel": "production",
      "env": {
        "APP_VARIANT": "production"
      }
    }
  },
  "submit": {
    "production": {
      "android": {
        "serviceAccountKeyPath": "./credentials/google-service-account.json",
        "track": "internal"
      },
      "ios": {
        "appleId": "apple-id@example.com",
        "ascAppId": "1234567890",
        "appleTeamId": "ABCDE12345"
      }
    }
  }
}
```

### 11.2 Build Commands

```bash
# Build for development (internal distribution)
eas build --profile development --platform all

# Build for staging
eas build --profile staging --platform all

# Build for production (App Store + Play Store)
eas build --profile production --platform all

# Submit to stores
eas submit --profile production --platform ios
eas submit --profile production --platform android
```

### 11.3 OTA Updates (Expo Updates)

For non-native-code changes (JS bundle only), use OTA updates to bypass store review:

```bash
# Push update to staging channel
eas update --branch staging --message "Fix attendance crash"

# Push update to production channel
eas update --branch production --message "Critical bug fix"
```

Use sparingly — store review (1-3 days) is still required for native module changes, new permissions, or major version bumps.

### 11.4 App Store Metadata

- **App Name**: "NMS — Nursery Management" (or "NMS Staff" / "NMS Parents" if separate apps)
- **Description**: Bilingual (English + Arabic).
- **Screenshots**: 6.7" iPhone + 5.5" iPhone + iPad 12.9" + Android phone + Android tablet.
- **Categories**: Education, Productivity.
- **Keywords**: nursery, childcare, daycare, early years, attendance, parent communication.
- **Privacy Policy URL**: https://nms.ae/privacy
- **Support URL**: https://nms.ae/support

---

## 12. Testing

### 12.1 Unit Tests (Jest)

```tsx
// __tests__/StudentCard.test.tsx
import { render, screen } from '@testing-library/react-native';
import { StudentCard } from '@/src/components/StudentCard';

describe('StudentCard', () => {
  it('should render student name', () => {
    render(<StudentCard student={{ firstName: 'Ahmed', lastName: 'Ali', ... }} />);
    expect(screen.getByText('Ahmed Ali')).toBeTruthy();
  });
});
```

### 12.2 E2E Tests (Detox)

```js
// e2e/attendance.spec.js
describe('Attendance', () => {
  beforeAll(async () => {
    await device.launchApp();
    await loginAsStaff();
  });

  it('should check in student via barcode', async () => {
    await element(by.id('attendance-tab')).tap();
    await element(by.id('scan-button')).tap();
    
    // Simulate barcode scan (mock camera in test)
    await device.scanBarcode('STU-12345');
    
    await expect(element(by.id('check-in-success'))).toBeVisible();
    await expect(element(by.text('Ahmed Ali')).withDescendant(by.text('Checked In'))).toBeVisible();
  });
});
```

### 12.3 Manual Test Matrix

Before each release:
- Test on iPhone 15 Pro (iOS 17), iPhone SE (iOS 16), Samsung Galaxy S23 (Android 14), iPad Pro (iPadOS 17).
- Test in English + Arabic (RTL layout).
- Test online + offline (airplane mode).
- Test push notifications (foreground, background, app killed).
- Test biometric unlock (Face ID, fingerprint).

---

## 13. App Store Submission

### 13.1 iOS App Store

1. Reserve App ID in Apple Developer Portal.
2. Create App Store Connect app entry.
3. Configure signing (EAS handles automatically).
4. Submit build via EAS Submit.
5. Fill App Store Connect metadata (description, screenshots, etc.).
6. Submit for review (1-3 days).
7. Address any review feedback.
8. Release.

**Common rejection reasons**:
- Missing privacy policy (we have it).
- Missing App Tracking Transparency prompt (only if we track — we don't, so skip).
- Test account credentials not provided (we provide a demo account).
- Crashes on launch (tested before submit).

### 13.2 Google Play Store

1. Create Google Play Developer account.
2. Create app entry.
3. Configure signing (EAS handles).
4. Submit build via EAS Submit to internal track.
5. Promote to closed testing → open testing → production.
6. Fill store listing (description, screenshots, etc.).
7. Submit for review (1-7 days).

---

## 14. App Variants

### 14.1 White-Label for Enterprise Customers

For Phase 3+ enterprise customers who want white-labeled app:

```ts
// app.config.ts
const variant = process.env.APP_VARIANT || 'nms-default';

const variants = {
  'nms-default': {
    name: 'NMS',
    bundleId: 'ae.nms.app',
    icon: './assets/icon.png',
    splash: './assets/splash.png',
  },
  'bright-futures': {
    name: 'Bright Futures',
    bundleId: 'ae.brightfutures.nursery',
    icon: './assets/clients/bright-futures/icon.png',
    splash: './assets/clients/bright-futures/splash.png',
  },
};

export default {
  expo: {
    name: variants[variant].name,
    ios: { bundleIdentifier: variants[variant].bundleId },
    android: { package: variants[variant].bundleId },
    icon: variants[variant].icon,
    splash: { image: variants[variant].splash },
  },
};
```

Build per variant: `APP_VARIANT=bright-futures eas build --profile production`.

### 14.2 On-Prem Customer Apps

For on-prem customers, app connects to their server URL:
- Configure via in-app settings (user enters server URL on first launch).
- OR pre-bake the URL into a customer-specific build variant.

---

**Next file to read**: `14-UX-UI-DESIGN.md` for the design system, parent.app baseline, and elevated UX patterns.
