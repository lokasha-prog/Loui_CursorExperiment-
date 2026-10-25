import { useCallback, useEffect, useState } from 'react'
import { Linking, Pressable, View } from 'react-native'
import { api, ApiError, isOffline } from '../api'
import { useAuth } from '../auth'
import { cached } from '../cache'
import { pick, useI18n } from '../i18n'
import { doOrQueue } from '../queue'
import { Ionicons } from '@expo/vector-icons'
import { Banner, Body, Btn, Card, Chip, Empty, Field, Heading, Hero, Pill, Row, Screen, Section, color } from '../ui'

/** Words for the operational screens, kept together so both languages stay in step. */
const words = {
  en: {
    back: 'Back', refresh: 'Pull down to refresh', none: 'Nothing here.', offline: 'No connection. Showing the last saved copy.', failed: 'Something went wrong. Try again.',
    children: 'Children', childrenHint: 'Find a child, see their health notes and call the family.', search: 'Search by name', allergies: 'Allergies', medical: 'Medical', diet: 'Diet', family: 'Family',
    call: 'Call', pickup: 'May collect', mainContact: 'Main contact', room: 'Room',
    leave: 'Leave and absence', leaveHint: 'Report a child absent and approve requests.', newLeave: 'Report absence', child: 'Child', type: 'Type', from: 'From (YYYY-MM-DD)', to: 'To (YYYY-MM-DD)', reason: 'Reason',
    submit: 'Send', approve: 'Approve', reject: 'Not approved', waiting: 'Waiting', sentOk: 'Sent.', savedLater: 'Saved on this phone. It will be sent when there is signal.',
    approvals: 'Posts to approve', approvalsHint: 'Photos and notes teachers want to share with families.', noPhotoPreview: 'Photos are shown in the web app.',
    enquiries: 'New families', enquiriesHint: 'Enquiries, calls to make and tours.', tours: 'Tours in the next 7 days', stage: 'Stage', logCall: 'Add a note', noteHint: 'What was said?', saveNote: 'Save note', stages: { no_stage: 'New', interested: 'Interested', contacted: 'Contacted', tour_appointment: 'Tour booked', toured: 'Toured', enrolled: 'Enrolled' },
    centre: 'Centre at a glance', centreHint: 'People, money and families in one view.', students: 'Children', staff: 'Staff', here: 'Here today', short: 'Rooms short', billed: 'Billed this month', collected: 'Collected this month', overdue: 'Overdue', open: 'Open enquiries', stale: 'Not contacted for 7 days', aed: 'AED',
    calendar: 'Calendar', calendarHint: 'Events and holidays for the next 30 days.', allDay: 'All day', eventTypes: { regular: 'Event', holiday: 'Holiday', birthday: 'Birthday', tour: 'Visit', leave: 'Leave' } as Record<string, string>, nextEvents: 'Coming up', dueTotal: 'Total still to pay', settled: 'Settled', toPay: 'To pay',
    invoices: 'Invoices and payments', invoicesHint: 'See what is due and pay by card.', due: 'Due', paid: 'Paid', total: 'Total', owed: 'Still to pay', payNow: 'Pay by card', paying: 'Opening the payment page...', payBack: 'Payment page opened. Come back here when you are done, then pull down to refresh.', payOff: 'Online payment is not switched on yet.',
    statusLabels: { issued: 'To pay', partially_paid: 'Part paid', paid: 'Paid', overdue: 'Overdue', draft: 'Draft', cancelled: 'Cancelled', submitted: 'Waiting', approved: 'Approved', rejected: 'Not approved' },
    newsletters: 'Newsletters', newslettersHint: 'News from the centre.', newNews: 'New', readBack: 'Back to the list',
  },
  ar: {
    back: 'رجوع', refresh: 'اسحب للأسفل للتحديث', none: 'لا يوجد شيء هنا.', offline: 'لا يوجد اتصال. نعرض آخر نسخة محفوظة.', failed: 'حدث خطأ. حاول مرة أخرى.',
    children: 'الأطفال', childrenHint: 'ابحث عن طفل، واطلع على ملاحظاته الصحية واتصل بأسرته.', search: 'ابحث بالاسم', allergies: 'الحساسية', medical: 'الحالة الصحية', diet: 'الحمية', family: 'الأسرة',
    call: 'اتصال', pickup: 'يمكنه الاستلام', mainContact: 'جهة الاتصال الرئيسية', room: 'الغرفة',
    leave: 'الإجازات والغياب', leaveHint: 'سجّل غياب طفل ووافق على الطلبات.', newLeave: 'تسجيل غياب', child: 'الطفل', type: 'النوع', from: 'من (سنة-شهر-يوم)', to: 'إلى (سنة-شهر-يوم)', reason: 'السبب',
    submit: 'إرسال', approve: 'موافقة', reject: 'عدم الموافقة', waiting: 'بالانتظار', sentOk: 'تم الإرسال.', savedLater: 'تم الحفظ على الهاتف. سيُرسل عند توفر الإشارة.',
    approvals: 'منشورات للموافقة', approvalsHint: 'صور وملاحظات يريد المعلمون مشاركتها مع الأسر.', noPhotoPreview: 'تظهر الصور في تطبيق الويب.',
    enquiries: 'أسر جديدة', enquiriesHint: 'الاستفسارات والاتصالات والجولات.', tours: 'جولات خلال 7 أيام', stage: 'المرحلة', logCall: 'إضافة ملاحظة', noteHint: 'ماذا قيل؟', saveNote: 'حفظ الملاحظة', stages: { no_stage: 'جديد', interested: 'مهتم', contacted: 'تم التواصل', tour_appointment: 'تم حجز جولة', toured: 'تمت الجولة', enrolled: 'مسجّل' },
    centre: 'المركز بنظرة', centreHint: 'الأشخاص والمال والأسر في عرض واحد.', students: 'الأطفال', staff: 'الموظفون', here: 'حاضرون اليوم', short: 'غرف ناقصة', billed: 'المفوتر هذا الشهر', collected: 'المحصّل هذا الشهر', overdue: 'المتأخر', open: 'استفسارات مفتوحة', stale: 'بلا تواصل منذ 7 أيام', aed: 'د.إ',
    calendar: 'التقويم', calendarHint: 'الفعاليات والعطلات خلال 30 يوماً.', allDay: 'طوال اليوم', eventTypes: { regular: 'فعالية', holiday: 'عطلة', birthday: 'عيد ميلاد', tour: 'زيارة', leave: 'إجازة' } as Record<string, string>, nextEvents: 'قريباً', dueTotal: 'إجمالي المتبقي للدفع', settled: 'مسددة', toPay: 'للدفع',
    invoices: 'الفواتير والمدفوعات', invoicesHint: 'اطلع على المستحق وادفع بالبطاقة.', due: 'الاستحقاق', paid: 'المدفوع', total: 'الإجمالي', owed: 'المتبقي', payNow: 'ادفع بالبطاقة', paying: 'جارٍ فتح صفحة الدفع...', payBack: 'فُتحت صفحة الدفع. عد إلى هنا بعد الانتهاء ثم اسحب للتحديث.', payOff: 'الدفع الإلكتروني غير مفعّل بعد.',
    statusLabels: { issued: 'للدفع', partially_paid: 'مدفوعة جزئياً', paid: 'مدفوعة', overdue: 'متأخرة', draft: 'مسودة', cancelled: 'ملغاة', submitted: 'بالانتظار', approved: 'تمت الموافقة', rejected: 'لم تتم الموافقة' },
    newsletters: 'النشرات', newslettersHint: 'أخبار من المركز.', newNews: 'جديد', readBack: 'العودة إلى القائمة',
  },
}

type Words = typeof words.en

function useWords(): Words {
  const { lang } = useI18n()
  return words[lang]
}

const today = () => new Date().toISOString().slice(0, 10)
const inDays = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString().slice(0, 10)
const money = (fils: number, lang: string) => new Intl.NumberFormat(lang === 'ar' ? 'ar-AE-u-nu-latn' : 'en-AE', { maximumFractionDigits: 2, minimumFractionDigits: 0 }).format(fils / 100)
const message = (e: unknown, w: Words) => (e instanceof ApiError && e.status !== 0 ? e.message : w.failed)

/** Loads a list with a saved copy, so a screen still opens with no signal. */
function useLoaded<T>(key: string, fetcher: () => Promise<T>, initial: T) {
  const [data, setData] = useState<T>(initial)
  const [busy, setBusy] = useState(true)
  const [offline, setOffline] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { lang } = useI18n()
  const load = useCallback(async () => {
    setBusy(true)
    setError(null)
    try {
      const r = await cached(key, fetcher)
      if (r.data !== null) setData(r.data)
      setOffline(r.offline)
    } catch (e) {
      if (isOffline(e)) setOffline(true)
      else setError(message(e, words[lang]))
    } finally {
      setBusy(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, lang])
  useEffect(() => { void load() }, [load])
  return { data, busy, offline, error, reload: load }
}

function Top({ title }: { title: string; onBack?: () => void; w?: Words }) {
  return <Hero title={title} />
}

function Status({ offline, error, w }: { offline: boolean; error: string | null; w: Words }) {
  return <>{offline && <Banner tone="info" text={w.offline} />}{error && <Banner tone="danger" text={error} />}</>
}

// ───────────────────────────────────────────────────────────── children

interface ChildRow { id: string; first_name: string; last_name: string; first_name_arabic: string | null; last_name_arabic: string | null; room_id: string | null; status: string }
interface ChildDetail extends ChildRow { allergy_info: string | null; medical_notes: string | null; dietary_requirements: string | null }
interface Guardian { id: string; first_name: string; last_name: string; phone: string | null; relationship: string; is_primary: boolean; can_pickup: boolean }

export function ChildrenOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState<string | null>(null)
  const list = useLoaded<ChildRow[]>(`ops.children.${search}`, () => api.page<ChildRow>(`/students?status=active&limit=100${search ? `&search=${encodeURIComponent(search)}` : ''}`), [])
  const name = (c: ChildRow) => (lang === 'ar' && c.first_name_arabic ? `${c.first_name_arabic} ${c.last_name_arabic ?? ''}` : `${c.first_name} ${c.last_name}`)

  if (open) return <ChildProfile id={open} onBack={() => setOpen(null)} />
  return (
    <Screen refreshing={list.busy} onRefresh={() => void list.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.children} />
      <Field label={w.search} value={search} onChangeText={setSearch} />
      <Status offline={list.offline} error={list.error} w={w} />
      {list.data.length === 0 && !list.busy ? <Empty text={w.none} /> : list.data.map((c) => (
        <Pressable key={c.id} accessibilityRole="button" onPress={() => setOpen(c.id)}><Card><Body bold>{name(c)}</Body></Card></Pressable>
      ))}
    </Screen>
  )
}

function ChildProfile({ id, onBack }: { id: string; onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const detail = useLoaded<ChildDetail | null>(`ops.child.${id}`, () => api.get<ChildDetail>(`/students/${id}`), null)
  const family = useLoaded<Guardian[]>(`ops.child.${id}.family`, () => api.page<Guardian>(`/students/${id}/guardians`), [])
  const c = detail.data
  return (
    <Screen refreshing={detail.busy} onRefresh={() => { void detail.reload(); void family.reload() }} onBack={onBack} backLabel={w.back}>
      <Top title={c ? (lang === 'ar' && (c as ChildRow).first_name_arabic ? `${(c as ChildRow).first_name_arabic} ${(c as ChildRow).last_name_arabic ?? ''}` : `${c.first_name} ${c.last_name}`) : '...'} />
      <Status offline={detail.offline} error={detail.error} w={w} />
      {c && (
        <Card>
          <Body>{w.allergies}: {c.allergy_info || '—'}</Body>
          <Body>{w.medical}: {c.medical_notes || '—'}</Body>
          <Body>{w.diet}: {c.dietary_requirements || '—'}</Body>
        </Card>
      )}
      <Heading>{w.family}</Heading>
      {family.data.map((g) => (
        <Card key={g.id}>
          <Body bold>{g.first_name} {g.last_name} · {g.relationship}</Body>
          <Row>{g.is_primary && <Pill tone="info" label={w.mainContact} />}{g.can_pickup && <Pill tone="ok" label={w.pickup} />}</Row>
          {g.phone ? <Btn kind="secondary" icon="call" label={`${w.call} ${g.phone}`} onPress={() => void Linking.openURL(`tel:${g.phone}`)} /> : null}
        </Card>
      ))}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── leave and absence

interface LeaveType { id: string; name: Record<string, string>; applies_to: string }
interface Leave { id: string; person_name: string | null; leave_type_name: Record<string, string>; start_date: string; end_date: string; reason: string | null; status: string }

export function LeaveOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const { can } = useAuth()
  const manage = can('attendance.override')
  const leaves = useLoaded<Leave[]>('ops.leaves', () => api.page<Leave>('/leaves?limit=50'), [])
  const types = useLoaded<LeaveType[]>('ops.leave.types', () => api.page<LeaveType>('/leave-types?limit=100'), [])
  const kids = useLoaded<ChildRow[]>('ops.leave.kids', () => api.page<ChildRow>('/students?status=active&limit=100'), [])
  const [childId, setChildId] = useState('')
  const [typeId, setTypeId] = useState('')
  const [from, setFrom] = useState(today())
  const [to, setTo] = useState(today())
  const [reason, setReason] = useState('')
  const [note, setNote] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const studentTypes = types.data.filter((x) => x.applies_to !== 'staff')

  const send = async () => {
    setBusy(true)
    setNote(null)
    try {
      const r = await doOrQueue('/leaves', { person_type: 'student', person_id: childId, leave_type_id: typeId || studentTypes[0]?.id, start_date: from, end_date: to, reason: reason || undefined }, 'leave')
      setNote(r === 'done' ? w.sentOk : w.savedLater)
      setReason('')
      if (r === 'done') void leaves.reload()
    } catch (e) {
      setNote(message(e, w))
    } finally {
      setBusy(false)
    }
  }

  const decide = async (id: string, action: 'approve' | 'reject') => {
    try { await api.post(`/leaves/${id}/actions/${action}`, {}); void leaves.reload() } catch (e) { setNote(message(e, w)) }
  }

  return (
    <Screen refreshing={leaves.busy} onRefresh={() => void leaves.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.leave} />
      <Status offline={leaves.offline} error={leaves.error} w={w} />
      {note && <Banner tone="info" text={note} />}
      <Card>
        <Heading>{w.newLeave}</Heading>
        <Body muted>{w.child}</Body>
        <Row>{kids.data.slice(0, 40).map((k) => <Chip key={k.id} active={childId === k.id} label={`${k.first_name} ${k.last_name[0] ?? ''}.`} onPress={() => setChildId(k.id)} />)}</Row>
        <Body muted>{w.type}</Body>
        <Row>{studentTypes.map((x) => <Chip key={x.id} active={(typeId || studentTypes[0]?.id) === x.id} label={pick(x.name, lang)} onPress={() => setTypeId(x.id)} />)}</Row>
        <Field label={w.from} value={from} onChangeText={setFrom} />
        <Field label={w.to} value={to} onChangeText={setTo} />
        <Field label={w.reason} value={reason} onChangeText={setReason} />
        <Btn label={w.submit} onPress={() => void send()} busy={busy} disabled={!childId || studentTypes.length === 0} />
      </Card>
      {leaves.data.map((l) => (
        <Card key={l.id}>
          <Row><Body bold>{l.person_name ?? ''}</Body><Pill tone={l.status === 'approved' ? 'ok' : l.status === 'rejected' ? 'danger' : 'info'} label={w.statusLabels[l.status as keyof typeof w.statusLabels] ?? l.status} /></Row>
          <Body muted>{pick(l.leave_type_name, lang)} · {l.start_date} → {l.end_date}</Body>
          {l.reason ? <Body>{l.reason}</Body> : null}
          {manage && l.status === 'submitted' && (
            <Row><Btn icon="checkmark" label={w.approve} onPress={() => void decide(l.id, 'approve')} /><Btn kind="danger" icon="close" label={w.reject} onPress={() => void decide(l.id, 'reject')} /></Row>
          )}
        </Card>
      ))}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── posts to approve

interface Post { id: string; type: string; caption: Record<string, string> | null; posted_by_name: string | null; media: unknown[]; created_at: string }

export function ApprovalsOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const posts = useLoaded<Post[]>('ops.approvals', () => api.page<Post>('/activities?status=pending&limit=50'), [])
  const [note, setNote] = useState<string | null>(null)
  const decide = async (id: string, action: 'approve' | 'reject') => {
    try { await api.post(`/activities/${id}/actions/${action}`, action === 'reject' ? { reason: 'Not suitable' } : undefined); void posts.reload() } catch (e) { setNote(message(e, w)) }
  }
  return (
    <Screen refreshing={posts.busy} onRefresh={() => void posts.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.approvals} />
      <Status offline={posts.offline} error={posts.error} w={w} />
      {note && <Banner tone="danger" text={note} />}
      {posts.data.length === 0 && !posts.busy ? <Empty text={w.none} /> : posts.data.map((p) => (
        <Card key={p.id}>
          <Row><Pill tone="info" label={p.type} /><Body muted>{p.posted_by_name ?? ''}</Body></Row>
          <Body>{pick(p.caption, lang)}</Body>
          {p.media.length > 0 && <Body muted>{p.media.length} · {w.noPhotoPreview}</Body>}
          <Row><Btn icon="checkmark" label={w.approve} onPress={() => void decide(p.id, 'approve')} /><Btn kind="danger" icon="close" label={w.reject} onPress={() => void decide(p.id, 'reject')} /></Row>
        </Card>
      ))}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── enquiries and tours

interface Enquiry { id: string; family_name: string; child_name: string; phone: string | null; stage: string; enquiry_date: string }
interface Tour { id: string; family_name: string; child_name: string; starts_at: string; status: string }
const STAGES = ['interested', 'contacted', 'tour_appointment', 'toured'] as const

export function EnquiriesOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { can } = useAuth()
  const write = can('enquiries.write')
  const list = useLoaded<Enquiry[]>('ops.enquiries', () => api.page<Enquiry>('/enquiries?limit=50'), [])
  const tours = useLoaded<Tour[]>('ops.tours', () => api.get<Tour[]>(`/tours?from=${today()}&to=${inDays(7)}`), [])
  const [open, setOpen] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [note, setNote] = useState<string | null>(null)
  const stage = (s: string) => w.stages[s as keyof typeof w.stages] ?? s

  const change = async (id: string, to: string) => {
    try { await api.post(`/enquiries/${id}/actions/change-stage`, { stage: to }); void list.reload() } catch (e) { setNote(message(e, w)) }
  }
  const addNote = async (id: string) => {
    try { await api.post(`/enquiries/${id}/activities`, { kind: 'call', summary: text.trim() }); setText(''); setNote(w.sentOk) } catch (e) { setNote(message(e, w)) }
  }

  return (
    <Screen refreshing={list.busy} onRefresh={() => { void list.reload(); void tours.reload() }} onBack={onBack} backLabel={w.back}>
      <Top title={w.enquiries} />
      <Status offline={list.offline} error={list.error} w={w} />
      {note && <Banner tone="info" text={note} />}
      {tours.data.length > 0 && (
        <Card>
          <Heading>{w.tours}</Heading>
          {tours.data.map((t) => <Body key={t.id}>{new Date(t.starts_at).toLocaleString()} · {t.family_name} ({t.child_name})</Body>)}
        </Card>
      )}
      {list.data.length === 0 && !list.busy ? <Empty text={w.none} /> : list.data.map((e) => (
        <Pressable key={e.id} accessibilityRole="button" onPress={() => setOpen(open === e.id ? null : e.id)}>
          <Card>
            <Row><Body bold>{e.family_name}</Body><Pill tone={e.stage === 'enrolled' ? 'ok' : 'info'} label={stage(e.stage)} /></Row>
            <Body muted>{e.child_name} · {e.enquiry_date}</Body>
            {open === e.id && (
              <View style={{ gap: 8 }}>
                {e.phone ? <Btn kind="secondary" icon="call" label={`${w.call} ${e.phone}`} onPress={() => void Linking.openURL(`tel:${e.phone}`)} /> : null}
                {write && (
                  <>
                    <Body muted>{w.stage}</Body>
                    <Row>{STAGES.map((s) => <Chip key={s} active={e.stage === s} label={stage(s)} onPress={() => void change(e.id, s)} />)}</Row>
                    <Field label={w.noteHint} value={text} onChangeText={setText} multiline />
                    <Btn label={w.saveNote} onPress={() => void addNote(e.id)} disabled={!text.trim()} />
                  </>
                )}
              </View>
            )}
          </Card>
        </Pressable>
      ))}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── centre at a glance (managers)

interface Overview {
  people: { students: number; staff: number; present_today: number; rooms_short: number } | null
  billing: { billed_fils: number; collected_fils: number; overdue_fils: number } | null
  admissions: { open_enquiries: number; stale_over7_days: number; tours_next7_days: number } | null
}

export function CentreOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const o = useLoaded<Overview | null>('ops.overview', () => api.get<Overview>('/dashboard/system'), null)
  const d = o.data
  const Stat = ({ label, value, alert }: { label: string; value: string | number; alert?: boolean }) => (
    <View style={{ minWidth: '45%', flexGrow: 1 }}><Heading>{alert ? `⚠ ${value}` : String(value)}</Heading><Body muted>{label}</Body></View>
  )
  return (
    <Screen refreshing={o.busy} onRefresh={() => void o.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.centre} />
      <Status offline={o.offline} error={o.error} w={w} />
      {d?.people && <Card><Row><Stat label={w.students} value={d.people.students} /><Stat label={w.staff} value={d.people.staff} /></Row><Row><Stat label={w.here} value={d.people.present_today} /><Stat label={w.short} value={d.people.rooms_short} alert={d.people.rooms_short > 0} /></Row></Card>}
      {d?.billing && <Card><Row><Stat label={`${w.billed} (${w.aed})`} value={money(d.billing.billed_fils, lang)} /><Stat label={`${w.collected} (${w.aed})`} value={money(d.billing.collected_fils, lang)} /></Row><Stat label={`${w.overdue} (${w.aed})`} value={money(d.billing.overdue_fils, lang)} alert={d.billing.overdue_fils > 0} /></Card>}
      {d?.admissions && <Card><Row><Stat label={w.open} value={d.admissions.open_enquiries} /><Stat label={w.stale} value={d.admissions.stale_over7_days} alert={d.admissions.stale_over7_days > 0} /></Row></Card>}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── calendar

interface Occurrence { event_id: string; title: Record<string, string>; description: Record<string, string> | null; starts_at: string; ends_at: string; is_all_day: boolean; type: string }

export function CalendarOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const events = useLoaded<Occurrence[]>('ops.calendar', () => api.get<Occurrence[]>(`/calendar/events?from=${today()}&to=${inDays(30)}`), [])
  const grouped = new Map<string, Occurrence[]>()
  for (const e of [...events.data].sort((a, b) => a.starts_at.localeCompare(b.starts_at))) {
    const day = e.starts_at.slice(0, 10)
    grouped.set(day, [...(grouped.get(day) ?? []), e])
  }
  const days = [...grouped.entries()]
  return (
    <Screen refreshing={events.busy} onRefresh={() => void events.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.calendar} />
      <Status offline={events.offline} error={events.error} w={w} />
      <Body muted>{w.calendarHint}</Body>
      {events.data.length === 0 && !events.busy ? <Empty text={w.none} /> : days.map(([day, list]) => (
        <View key={day} style={{ gap: 8 }}>
          <Section>{new Date(`${day}T12:00:00`).toLocaleDateString(lang === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { weekday: 'long', day: 'numeric', month: 'long' })}</Section>
          {list.map((e, i) => (
            <Card key={`${e.event_id}${i}`}>
              <Row><Body bold>{pick(e.title, lang)}</Body><Pill tone={e.type === 'holiday' ? 'warn' : 'info'} label={w.eventTypes[e.type] ?? e.type} /></Row>
              <Body muted>{e.is_all_day ? w.allDay : new Date(e.starts_at).toLocaleTimeString(lang === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { hour: '2-digit', minute: '2-digit' })}</Body>
              {e.description ? <Body>{pick(e.description, lang)}</Body> : null}
            </Card>
          ))}
        </View>
      ))}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── invoices and paying

interface Invoice { id: string; invoice_number: string; student_name: string | null; due_date: string; total_fils: number; paid_fils: number; status: string }

export function InvoicesOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const { me } = useAuth()
  const family = !!me?.roles.includes('parent')
  const list = useLoaded<Invoice[]>('ops.invoices', () => api.page<Invoice>('/invoices?limit=50'), [])
  const config = useLoaded<{ enabled: boolean } | null>('ops.pay.config', () => api.get<{ enabled: boolean }>('/billing/payments/config'), null)
  const [note, setNote] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const isOpen = (i: Invoice) => i.total_fils - i.paid_fils > 0 && ['issued', 'partially_paid', 'overdue'].includes(i.status)
  const ordered = [...list.data].sort((a, b) => Number(isOpen(b)) - Number(isOpen(a)) || a.due_date.localeCompare(b.due_date))
  const totalOwed = ordered.filter(isOpen).reduce((sum, i) => sum + i.total_fils - i.paid_fils, 0)

  const pay = async (id: string) => {
    setBusy(id)
    setNote(w.paying)
    try {
      const started = await api.post<{ redirect_url: string | null; status: string }>(`/billing/invoices/${id}/pay`, {})
      if (started.redirect_url) {
        await Linking.openURL(started.redirect_url)
        setNote(w.payBack)
      } else {
        setNote(w.sentOk)
        void list.reload()
      }
    } catch (e) {
      setNote(message(e, w))
    } finally {
      setBusy(null)
    }
  }

  return (
    <Screen refreshing={list.busy} onRefresh={() => void list.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.invoices} />
      <Status offline={list.offline} error={list.error} w={w} />
      {note && <Banner tone="info" text={note} />}
      {family && config.data && !config.data.enabled && <Body muted>{w.payOff}</Body>}
      {family && totalOwed > 0 && <Card><Body muted>{w.dueTotal}</Body><Heading>{w.aed} {money(totalOwed, lang)}</Heading></Card>}
      {list.data.length === 0 && !list.busy ? <Empty text={w.none} /> : ordered.map((i, index) => {
        const owed = i.total_fils - i.paid_fils
        const open = owed > 0 && ['issued', 'partially_paid', 'overdue'].includes(i.status)
        const heading = index === 0 || isOpen(ordered[index - 1]) !== open ? <Section>{open ? w.toPay : w.settled}</Section> : null
        return (
          <View key={i.id} style={{ gap: 8 }}>
          {heading}
          <Card>
            <Row><Body bold>{i.invoice_number}</Body><Pill tone={i.status === 'paid' ? 'ok' : i.status === 'overdue' ? 'danger' : 'info'} label={w.statusLabels[i.status as keyof typeof w.statusLabels] ?? i.status} /></Row>
            <Body muted>{i.student_name ?? ''} · {w.due} {i.due_date}</Body>
            <Body>{w.total}: {w.aed} {money(i.total_fils, lang)}  ·  {w.paid}: {money(i.paid_fils, lang)}</Body>
            {open && <Body bold>{w.owed}: {w.aed} {money(owed, lang)}</Body>}
            {open && family && config.data?.enabled && <Btn icon="card" label={w.payNow} onPress={() => void pay(i.id)} busy={busy === i.id} />}
          </Card>
          </View>
        )
      })}
    </Screen>
  )
}

// ───────────────────────────────────────────────────────────── newsletters

interface Newsletter { id: string; title: Record<string, string>; published_at: string | null; opened: boolean }
interface ReadNews { id: string; title: Record<string, string>; body: string }

const plain = (html: string) => html.replace(/<br\s*\/?>/gi, '\n').replace(/<\/(p|div|h\d|li)>/gi, '\n').replace(/<[^>]+>/g, '').replace(/&nbsp;/g, ' ').replace(/&amp;/g, '&').replace(/\n{3,}/g, '\n\n').trim()

export function NewslettersOps({ onBack }: { onBack: () => void }) {
  const w = useWords()
  const { lang } = useI18n()
  const list = useLoaded<Newsletter[]>('ops.news', () => api.page<Newsletter>('/newsletters/mine?limit=30'), [])
  const [open, setOpen] = useState<string | null>(null)
  const issue = useLoaded<ReadNews | null>(`ops.news.${open}.${lang}`, () => (open ? api.get<ReadNews>(`/newsletters/${open}/read?language=${lang}`) : Promise.resolve(null)), null)
  if (open) {
    return (
      <Screen refreshing={issue.busy} onBack={() => { setOpen(null); void list.reload() }} backLabel={w.back}>
        <Top title={issue.data ? pick(issue.data.title, lang) : '...'} />
        <Status offline={issue.offline} error={issue.error} w={w} />
        {issue.data && <Card><Body>{plain(issue.data.body)}</Body></Card>}
      </Screen>
    )
  }
  return (
    <Screen refreshing={list.busy} onRefresh={() => void list.reload()} onBack={onBack} backLabel={w.back}>
      <Top title={w.newsletters} />
      <Status offline={list.offline} error={list.error} w={w} />
      {list.data.length === 0 && !list.busy ? <Empty text={w.none} /> : list.data.map((n) => (
        <Pressable key={n.id} accessibilityRole="button" onPress={() => setOpen(n.id)}>
          <Card><Row><Body bold>{pick(n.title, lang)}</Body>{!n.opened && <Pill tone="info" label={w.newNews} />}</Row><Body muted>{n.published_at ? new Date(n.published_at).toLocaleDateString() : ''}</Body></Card>
        </Pressable>
      ))}
    </Screen>
  )
}

export const opsWords = (lang: 'en' | 'ar') => words[lang]
