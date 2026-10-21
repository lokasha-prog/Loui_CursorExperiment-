import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { I18nManager, Platform } from 'react-native'
import { store } from './core'

export type Lang = 'en' | 'ar'

const en = {
  appName: 'Bright Futures', staffApp: 'Staff app', email: 'E-mail', password: 'Password', signIn: 'Sign in', signOut: 'Sign out', signingIn: 'Signing in...', chooseCentre: 'Choose your centre',
  signInFailed: 'Please check your e-mail and password.', noConnection: 'No connection. Showing what was saved.', biometric: 'Unlock with fingerprint or face', biometricOn: 'Fingerprint / face unlock', unlock: 'Unlock',
  tabs: { dashboard: 'Today', attendance: 'Attendance', feed: 'Feed', messages: 'Messages', more: 'More', reports: 'Day report', alerts: 'Alerts', learning: 'Learning', observe: 'Observe' },
  today: 'Today', children: 'Children', present: 'Present', absent: 'Absent', notArrived: 'Not yet here', onLeave: 'On leave', staffPresent: 'Staff here', roomsShort: 'Rooms short of staff', pendingLeave: 'Leave requests waiting',
  ratio: 'Staff cover', short: 'Short', ok: 'Covered', staffNeeded: '{{present}} here, {{required}} needed', childrenHere: '{{count}} children',
  scan: 'Scan a child or staff card', typeCode: 'Or type the number', checkIn: 'Check in', checkOut: 'Check out', checkedIn: 'Checked in', checkedOut: 'Checked out', savedForLater: 'Saved. It will be sent when there is signal.',
  scanHint: 'Point the camera at the QR code on the card', openCamera: 'Open camera', closeCamera: 'Close camera', cameraNeeded: 'Camera permission is needed to scan.', scanned: 'Scanned',
  list: 'Today\'s list', nobody: 'Nobody has checked in yet.', waiting: '{{count}} waiting to send', sendNow: 'Send now', sent: '{{count}} sent', dropped: '{{count}} could not be sent',
  status: { present: 'Present', late: 'Late', absent: 'Absent', early_leave: 'Left early', pending: 'Not yet' },
  newPost: 'New post', postType: 'Type', caption: 'What happened', post: 'Post', posted: 'Posted. It will be shown to parents after approval.', types: { diary: 'Diary', food: 'Meal', toilet: 'Toilet', mood: 'Mood', announcement: 'Announcement' },
  noPosts: 'No posts yet.', approval: { pending: 'Waiting for approval', approved: 'Approved', rejected: 'Not approved' },
  messages: 'Messages', noMessages: 'No messages yet.', reply: 'Reply', send: 'Send', unread: 'New', typeMessage: 'Write a message',
  alerts: 'Alerts', noAlerts: 'No alerts.', critical: 'Urgent',
  more: 'More', language: 'Language', english: 'English', arabic: 'العربية', profile: 'Your account',
  emergency: 'Emergency information', emergencyBody: 'Allergies, medical notes and parent phone numbers for every child. Saved on this phone so it works with no signal.',
  updated: 'Updated {{when}}', refresh: 'Refresh now', refreshing: 'Refreshing...', noCache: 'Not saved on this phone yet. Connect and press Refresh now.', call: 'Call', allergies: 'Allergies', medical: 'Medical', dietary: 'Diet', contacts: 'Parents and carers', pickup: 'May collect', primary: 'Main contact', searchChild: 'Search a child',
  retry: 'Try again', loading: 'Loading...', cancel: 'Cancel', close: 'Close', back: 'Back',
  briefing: 'AI briefing for management', briefingRun: 'Write briefing', briefingBusy: 'Reading the figures...', briefingDown: 'The AI Brain is not available right now.',
  learningTitle: 'Learning journey', pickChild: 'Choose a child', noChildren: 'No children linked to this account.', learnReports: 'Reports', learnTimeline: 'Moments and reports', noLearning: 'Nothing here yet. Moments and reports will appear as they are shared.',
  levels: { secure: 'Going strong', growing: 'Growing well', starting: 'Just starting' }, kindMoment: 'Moment', kindReport: 'Report',
  observe: 'Record an observation', obsType: 'Type', obsTypes: { informal: 'Informal', formal: 'Formal', concern: 'Concern' }, obsNotes: 'What you saw', obsShare: 'Share with the family', obsConcernPrivate: 'Concerns are never shared with the family',
  obsSave: 'Save observation', obsSaved: 'Observation saved.', obsQueued: 'Saved on this phone. It will be sent when there is signal.', obsPhoto: 'Add a photo', obsPhotoTaken: 'Photo added', obsPhotoNeedsSignal: 'The photo needs a signal, so only the note was saved.', takePhoto: 'Take photo', choosePhoto: 'Choose photo', search: 'Search',
  updatedOffline: 'Showing the last saved copy.', unsent: '{{count}} waiting to be sent',
  serverAddress: 'Nursery server address', serverHint: 'Your nursery gives you this address, for example http://192.168.1.20:5178', serverSave: 'Save address', serverSaved: 'Saved',
  noReports: 'No day reports yet.', arrived: 'Arrived', left: 'Left', moments: '{{count}} moments shared', markAllRead: 'Mark all as read', preferences: 'How we reach you', inApp: 'In the app', byEmail: 'By e-mail',
  tiers: { critical: 'Urgent alerts', operational: 'Daily updates', informational: 'News and reminders' }, urgentLocked: 'Urgent alerts always reach you.', pushOn: 'Phone alerts', pushOff: 'Phone alerts are off', pushEnable: 'Turn on phone alerts',
}

const ar: typeof en = {
  appName: 'برايت فيوتشرز', staffApp: 'تطبيق الموظفين', email: 'البريد الإلكتروني', password: 'كلمة المرور', signIn: 'تسجيل الدخول', signOut: 'تسجيل الخروج', signingIn: 'جارٍ الدخول...', chooseCentre: 'اختر مركزك',
  signInFailed: 'يرجى التحقق من البريد الإلكتروني وكلمة المرور.', noConnection: 'لا يوجد اتصال. نعرض ما تم حفظه.', biometric: 'افتح بالبصمة أو الوجه', biometricOn: 'فتح القفل بالبصمة / الوجه', unlock: 'فتح',
  tabs: { dashboard: 'اليوم', attendance: 'الحضور', feed: 'الخلاصة', messages: 'الرسائل', more: 'المزيد', reports: 'تقرير اليوم', alerts: 'التنبيهات', learning: 'التعلّم', observe: 'ملاحظة' },
  today: 'اليوم', children: 'الأطفال', present: 'حاضر', absent: 'غائب', notArrived: 'لم يصل بعد', onLeave: 'في إجازة', staffPresent: 'الموظفون الحاضرون', roomsShort: 'غرف ينقصها موظفون', pendingLeave: 'طلبات إجازة بالانتظار',
  ratio: 'تغطية الموظفين', short: 'ناقص', ok: 'مغطّى', staffNeeded: 'الحاضر {{present}}، المطلوب {{required}}', childrenHere: '{{count}} أطفال',
  scan: 'امسح بطاقة طفل أو موظف', typeCode: 'أو اكتب الرقم', checkIn: 'تسجيل دخول', checkOut: 'تسجيل خروج', checkedIn: 'تم تسجيل الدخول', checkedOut: 'تم تسجيل الخروج', savedForLater: 'تم الحفظ. سيُرسل عند توفر الإشارة.',
  scanHint: 'وجّه الكاميرا نحو رمز QR على البطاقة', openCamera: 'فتح الكاميرا', closeCamera: 'إغلاق الكاميرا', cameraNeeded: 'نحتاج إذن الكاميرا للمسح.', scanned: 'تم المسح',
  list: 'قائمة اليوم', nobody: 'لم يسجل أحد الدخول بعد.', waiting: '{{count}} بانتظار الإرسال', sendNow: 'أرسل الآن', sent: 'أُرسل {{count}}', dropped: 'تعذر إرسال {{count}}',
  status: { present: 'حاضر', late: 'متأخر', absent: 'غائب', early_leave: 'غادر مبكراً', pending: 'لم يصل' },
  newPost: 'منشور جديد', postType: 'النوع', caption: 'ماذا حدث', post: 'نشر', posted: 'تم النشر. سيظهر لأولياء الأمور بعد الموافقة.', types: { diary: 'يوميات', food: 'وجبة', toilet: 'دورة المياه', mood: 'المزاج', announcement: 'إعلان' },
  noPosts: 'لا توجد منشورات بعد.', approval: { pending: 'بانتظار الموافقة', approved: 'تمت الموافقة', rejected: 'لم تتم الموافقة' },
  messages: 'الرسائل', noMessages: 'لا توجد رسائل بعد.', reply: 'رد', send: 'إرسال', unread: 'جديد', typeMessage: 'اكتب رسالة',
  alerts: 'التنبيهات', noAlerts: 'لا توجد تنبيهات.', critical: 'عاجل',
  more: 'المزيد', language: 'اللغة', english: 'English', arabic: 'العربية', profile: 'حسابك',
  emergency: 'معلومات الطوارئ', emergencyBody: 'الحساسية والملاحظات الطبية وأرقام أولياء الأمور لكل طفل. محفوظة على هذا الهاتف لتعمل بدون إشارة.',
  updated: 'آخر تحديث {{when}}', refresh: 'حدّث الآن', refreshing: 'جارٍ التحديث...', noCache: 'غير محفوظة على هذا الهاتف بعد. اتصل واضغط حدّث الآن.', call: 'اتصال', allergies: 'الحساسية', medical: 'طبي', dietary: 'الغذاء', contacts: 'أولياء الأمور والمرافقون', pickup: 'يمكنه الاستلام', primary: 'جهة الاتصال الرئيسية', searchChild: 'ابحث عن طفل',
  retry: 'حاول مرة أخرى', loading: 'جارٍ التحميل...', cancel: 'إلغاء', close: 'إغلاق', back: 'رجوع',
  briefing: 'موجز الذكاء الاصطناعي للإدارة', briefingRun: 'اكتب الموجز', briefingBusy: 'جارٍ قراءة الأرقام...', briefingDown: 'العقل الذكي غير متاح الآن.',
  learningTitle: 'رحلة التعلّم', pickChild: 'اختر طفلاً', noChildren: 'لا يوجد أطفال مرتبطون بهذا الحساب.', learnReports: 'التقارير', learnTimeline: 'لحظات وتقارير', noLearning: 'لا شيء هنا بعد. ستظهر اللحظات والتقارير عند مشاركتها.',
  levels: { secure: 'متمكّن', growing: 'ينمو بشكل جيد', starting: 'في البداية' }, kindMoment: 'لحظة', kindReport: 'تقرير',
  observe: 'تسجيل ملاحظة', obsType: 'النوع', obsTypes: { informal: 'غير رسمية', formal: 'رسمية', concern: 'قلق' }, obsNotes: 'ما رأيته', obsShare: 'شاركها مع الأسرة', obsConcernPrivate: 'لا تُشارَك حالات القلق مع الأسرة أبداً',
  obsSave: 'حفظ الملاحظة', obsSaved: 'تم حفظ الملاحظة.', obsQueued: 'تم الحفظ على هذا الهاتف. ستُرسل عند توفر الإشارة.', obsPhoto: 'أضف صورة', obsPhotoTaken: 'تمت إضافة الصورة', obsPhotoNeedsSignal: 'الصورة تحتاج إشارة، لذا حُفظت الملاحظة فقط.', takePhoto: 'التقاط صورة', choosePhoto: 'اختيار صورة', search: 'بحث',
  updatedOffline: 'نعرض آخر نسخة محفوظة.', unsent: '{{count}} بانتظار الإرسال',
  serverAddress: 'عنوان خادم الحضانة', serverHint: 'تعطيك الحضانة هذا العنوان، مثل http://192.168.1.20:5178', serverSave: 'حفظ العنوان', serverSaved: 'تم الحفظ',
  noReports: 'لا توجد تقارير يومية بعد.', arrived: 'وصل', left: 'غادر', moments: 'تمت مشاركة {{count}} لحظات', markAllRead: 'تحديد الكل كمقروء', preferences: 'كيف نتواصل معك', inApp: 'داخل التطبيق', byEmail: 'بالبريد الإلكتروني',
  tiers: { critical: 'تنبيهات عاجلة', operational: 'تحديثات يومية', informational: 'أخبار وتذكيرات' }, urgentLocked: 'التنبيهات العاجلة تصلك دائماً.', pushOn: 'تنبيهات الهاتف', pushOff: 'تنبيهات الهاتف متوقفة', pushEnable: 'تفعيل تنبيهات الهاتف',
}

export const dictionaries = { en, ar }

type Dict = typeof en
type Leaf<T, P extends string = ''> = { [K in keyof T & string]: T[K] extends string ? `${P}${K}` : Leaf<T[K], `${P}${K}.`> }[keyof T & string]
export type Key = Leaf<Dict>

function lookup(dict: Dict, key: string): string {
  const value = key.split('.').reduce<unknown>((o, k) => (o as Record<string, unknown> | undefined)?.[k], dict)
  return typeof value === 'string' ? value : key
}

interface I18n {
  lang: Lang
  rtl: boolean
  setLang: (lang: Lang) => void
  t: (key: Key, vars?: Record<string, string | number>) => string
  /** Row direction that follows the language (a row reads right to left in Arabic). */
  row: 'row' | 'row-reverse'
  align: 'left' | 'right'
}

const Context = createContext<I18n | null>(null)

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>('en')

  useEffect(() => {
    void store.get<Lang>('nms.lang').then((saved) => saved && setLangState(saved))
  }, [])

  const value = useMemo<I18n>(() => {
    const rtl = lang === 'ar'
    if (Platform.OS === 'web' && typeof document !== 'undefined') {
      document.documentElement.dir = rtl ? 'rtl' : 'ltr'
      document.documentElement.lang = lang
    }
    return {
      lang,
      rtl,
      setLang: (next) => { setLangState(next); void store.set('nms.lang', next); I18nManager.allowRTL(next === 'ar') },
      t: (key, vars) => Object.entries(vars ?? {}).reduce((text, [k, v]) => text.replaceAll(`{{${k}}}`, String(v)), lookup(dictionaries[lang], key)),
      row: rtl ? 'row-reverse' : 'row',
      align: rtl ? 'right' : 'left',
    }
  }, [lang])

  return <Context.Provider value={value}>{children}</Context.Provider>
}

export function useI18n() {
  const ctx = useContext(Context)
  if (!ctx) throw new Error('useI18n must be used inside I18nProvider')
  return ctx
}

export const pick = (text: Record<string, string> | null | undefined, lang: Lang) => (text ? text[lang] ?? text.en ?? Object.values(text)[0] ?? '' : '')
