# App Store and Google Play submission pack

The phone app (`nms/mobile`, Expo) is ready to be built. This pack holds everything the stores ask for. Items marked **OWNER** need an account, a decision or a real device.

## 1. Before the first build (OWNER)
| Item | Who | Notes |
|---|---|---|
| Apple Developer Program membership (USD 99 per year) | Owner | Needed for the App Store and for push alerts on iPhone |
| Google Play Console account (one-off USD 25) | Owner | |
| Expo account and project (`eas init`) | Dev | Gives the project id; put it in `app.json` under `extra.eas.projectId` (needed for push alerts) |
| Final app name, icon, splash | Owner | Placeholder bundle id `com.mobilityeye.brightfutures` |
| Server address (HTTPS) | Owner/IT | Replace `REPLACE-WITH-YOUR-SERVER` in `mobile/eas.json` |
| Set `Push__Provider=expo` on the server | IT | Turns phone alerts on |

## 2. Build and submit
```bash
cd nms/mobile
npx eas login
npx eas build --platform all --profile production      # builds both stores' files
npx eas submit --platform ios                          # uploads to App Store Connect
npx eas submit --platform android                      # uploads to Google Play (internal track first)
```
Test first with `--profile preview` (an installable APK for Android; TestFlight for iPhone). Never ship the test-user buttons: they only appear when `EXPO_PUBLIC_DEV_LOGIN=1`, which the production profile does not set.

## 3. Store listing (English)
**Name:** Bright Futures (staff and parents)
**Subtitle (30):** Your nursery, in your pocket
**Short description (80):** See your child's day and learning, message the team, and run attendance.
**Description:**
Bright Futures connects nurseries and families.
Parents: photos, meals, naps and a daily report as they happen; your child's learning journey and approved termly reports; messages with the team; invoices and forms; alerts you choose, in English or Arabic.
Staff: scan children in and out with the camera, even with no signal; see live staff cover; post updates; record observations with a photo; read emergency information saved on the phone.
Works with your nursery's own Bright Futures system. You need an account from your nursery.
**Keywords:** nursery, daycare, parents, early years, EYFS, attendance, learning journey
**Category:** Education (Parenting as second choice). **Age rating:** 4+ (no objectionable content; accounts are for adults).

## 4. Store listing (Arabic)
**الاسم:** برايت فيوتشرز (للموظفين وأولياء الأمور)
**العنوان الفرعي:** حضانتك في جيبك
**وصف قصير:** شاهد يوم طفلك وتعلّمه، راسل الفريق، وأدر الحضور.
**الوصف:**
يربط برايت فيوتشرز الحضانات بالأسر.
لأولياء الأمور: صور ووجبات وقيلولة وتقرير يومي لحظة حدوثها؛ رحلة تعلّم طفلك وتقاريره الفصلية المعتمدة؛ رسائل مع الفريق؛ الفواتير والنماذج؛ وتنبيهات تختارها، بالعربية أو الإنجليزية.
للموظفين: مسح دخول الأطفال وخروجهم بالكاميرا حتى بدون إشارة؛ متابعة تغطية الموظفين لحظياً؛ نشر المستجدات؛ تسجيل الملاحظات مع صورة؛ وقراءة معلومات الطوارئ المحفوظة على الهاتف.
يعمل مع نظام برايت فيوتشرز الخاص بحضانتك. تحتاج إلى حساب من حضانتك.

## 5. Privacy answers (both stores)
- **Data collected:** name, e-mail, phone, children's names, photos and learning notes entered by the nursery; device push token; app activity needed to run the service.
- **Used for:** running the nursery service only. **No advertising, no tracking, no sale of data.**
- **Shared with:** the nursery that created the account; push delivery provider (Expo/Apple/Google) receives the alert title and text and a device token.
- **Security:** encrypted in transit (HTTPS); refresh token in the phone's secure storage; optional fingerprint/face unlock; saved copies wiped at sign-out.
- **Deletion:** the nursery administrator removes accounts and children's data; parents ask the nursery (UAE data-protection law applies). Put the public privacy-policy address in both stores (OWNER: legal text to be approved).
- **Permissions and why:** camera (scan cards, take photos), notifications (alerts), Face ID / biometrics (unlock). Texts are in `app.json`.

## 6. Reviewer notes (both stores)
The app needs an account created by a nursery. Provide the reviewers a **dedicated demo centre** with one parent and one teacher account (OWNER creates these; never use real families). Explain: "Sign in with the parent account to see the feed, reports and learning journey; sign in with the teacher account to scan and record observations."

## 7. Screenshots to capture (OWNER or dev with a real device or simulator)
Parent: feed, day report, learning journey, messages, alerts. Staff: today, scan, observe, emergency info. Capture each in English and Arabic (right-to-left), for 6.7-inch and 5.5-inch iPhone and a 6-inch Android phone.

## 8. Push alerts for every tier
The server sends one phone alert per registered phone for every in-app notice, tagged with its tier. On Android the app creates three channels: **Urgent** (sound and pop-up), **Daily updates**, **News and reminders** (quiet). Tapping an alert opens the screen it is about. Parents choose which tiers they get in Alerts; urgent alerts cannot be switched off.

## 9. Release checklist
- [ ] Accounts and project id in place; server address set; `Push__Provider=expo`
- [ ] Preview build installed on one Android and one iPhone; sign in, scan, post offline then reconnect, receive a push, tap it
- [ ] Arabic right-to-left checked on both phones
- [ ] Privacy policy address live; listing text and screenshots uploaded
- [ ] Production build submitted; internal track / TestFlight pilot with the pilot nursery for one week
- [ ] Release to the public stores; note the version in the change log
