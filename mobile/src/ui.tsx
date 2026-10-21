import { Ionicons } from '@expo/vector-icons'
import { type ReactNode } from 'react'
import { ActivityIndicator, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View, type TextInputProps, type ViewStyle } from 'react-native'
import { useI18n } from './i18n'

/** One palette for the whole app: calm teal for trust, warm sunshine and coral for the nursery feel. */
export const color = {
  primary: '#0E7C86', primaryDark: '#0A4F57', primarySoft: '#E2F3F4', accent: '#F2766B', accentSoft: '#FDE8E5', sun: '#F5B544', sunSoft: '#FEF1D6',
  sky: '#4F9DE0', skySoft: '#E0EEFB', lilac: '#7C6FE0', lilacSoft: '#E9E6FB', mint: '#2FB68A', mintSoft: '#DDF5EC',
  ink: '#16263A', muted: '#6B7A8D', line: '#E6EBF1', bg: '#F3F6F9', white: '#FFFFFF', field: '#EEF2F6',
  danger: '#C0392B', dangerBg: '#FDE7E4', ok: '#067A55', okBg: '#DDF5EC', warn: '#A9640A', warnBg: '#FEF1D6', info: '#1F5FBF', infoBg: '#E0EEFB',
}

export type Tone = 'primary' | 'accent' | 'sun' | 'sky' | 'lilac' | 'mint'
const toneColors: Record<Tone, [string, string]> = {
  primary: [color.primarySoft, color.primary], accent: [color.accentSoft, color.accent], sun: [color.sunSoft, '#C98A12'],
  sky: [color.skySoft, color.sky], lilac: [color.lilacSoft, color.lilac], mint: [color.mintSoft, color.mint],
}

const shadow = Platform.select<ViewStyle>({
  ios: { shadowColor: '#0F2A43', shadowOpacity: 0.08, shadowRadius: 14, shadowOffset: { width: 0, height: 6 } },
  android: { elevation: 3 },
  default: { shadowColor: '#0F2A43', shadowOpacity: 0.08, shadowRadius: 14, shadowOffset: { width: 0, height: 6 } },
}) as ViewStyle

const s = StyleSheet.create({
  screen: { flex: 1, backgroundColor: color.bg },
  content: { padding: 18, gap: 14, paddingBottom: 40 },
  card: { backgroundColor: color.white, borderRadius: 22, padding: 16, gap: 8, ...shadow },
  title: { fontSize: 26, fontWeight: '800', color: color.ink, letterSpacing: -0.3 },
  heading: { fontSize: 17, fontWeight: '700', color: color.ink },
  body: { fontSize: 15, color: color.ink, lineHeight: 21 },
  muted: { fontSize: 13, color: color.muted, lineHeight: 18 },
  btn: { minHeight: 52, borderRadius: 18, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 20, flexDirection: 'row', gap: 8 },
  btnText: { fontSize: 15, fontWeight: '700' },
  input: { minHeight: 52, borderRadius: 16, backgroundColor: color.field, paddingHorizontal: 16, fontSize: 16, color: color.ink, borderWidth: 1.5, borderColor: 'transparent' },
  pill: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: 11, paddingVertical: 4 },
  pillText: { fontSize: 12, fontWeight: '700' },
})

export function Screen({ children, refreshing, onRefresh, onBack, backLabel }: { children: ReactNode; refreshing?: boolean; onRefresh?: () => void; onBack?: () => void; backLabel?: string }) {
  const { lang, row } = useI18n()
  return (
    <View style={{ flex: 1, backgroundColor: color.bg }}>
      <ScrollView
        style={s.screen}
        contentContainerStyle={[s.content, onBack && { paddingBottom: 110 }]}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
        refreshControl={onRefresh ? <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} tintColor={color.primary} /> : undefined}
      >
        {children}
      </ScrollView>
      {onBack && (
        <View pointerEvents="box-none" style={{ position: 'absolute', left: 0, right: 0, bottom: 16, alignItems: 'center' }}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={backLabel ?? 'Back'}
            onPress={onBack}
            style={({ pressed }) => [{ flexDirection: row, alignItems: 'center', gap: 8, minHeight: 54, paddingHorizontal: 26, borderRadius: 27, backgroundColor: color.primaryDark, opacity: pressed ? 0.9 : 1 }, shadow]}
          >
            <Ionicons name={lang === 'ar' ? 'arrow-forward' : 'arrow-back'} size={20} color={color.white} />
            <Text style={{ color: color.white, fontSize: 15, fontWeight: '800' }}>{backLabel ?? 'Back'}</Text>
          </Pressable>
        </View>
      )}
    </View>
  )
}

export function Card({ children, style }: { children: ReactNode; style?: ViewStyle }) {
  return <View style={[s.card, style]}>{children}</View>
}

export function Title({ children }: { children: ReactNode }) {
  const { align } = useI18n()
  return <Text accessibilityRole="header" style={[s.title, { textAlign: align }]}>{children}</Text>
}

export function Heading({ children }: { children: ReactNode }) {
  const { align } = useI18n()
  return <Text accessibilityRole="header" style={[s.heading, { textAlign: align }]}>{children}</Text>
}

export function Body({ children, muted, bold }: { children: ReactNode; muted?: boolean; bold?: boolean }) {
  const { align } = useI18n()
  return <Text style={[muted ? s.muted : s.body, bold && { fontWeight: '700' }, { textAlign: align }]}>{children}</Text>
}

type IconName = keyof typeof Ionicons.glyphMap

/** A round coloured badge holding an icon; the app's main visual cue. */
export function IconChip({ name, tone = 'primary', size = 44 }: { name: IconName; tone?: Tone; size?: number }) {
  const [bg, fg] = toneColors[tone]
  return (
    <View accessibilityElementsHidden importantForAccessibility="no-hide-descendants" style={{ width: size, height: size, borderRadius: size * 0.34, backgroundColor: bg, alignItems: 'center', justifyContent: 'center' }}>
      <Ionicons name={name} size={size * 0.52} color={fg} />
    </View>
  )
}

export function Btn({ label, onPress, kind = 'primary', busy, disabled, icon }: { label: string; onPress: () => void; kind?: 'primary' | 'secondary' | 'danger'; busy?: boolean; disabled?: boolean; icon?: IconName }) {
  const bg = kind === 'primary' ? color.primary : kind === 'danger' ? color.dangerBg : color.primarySoft
  const fg = kind === 'primary' ? color.white : kind === 'danger' ? color.danger : color.primaryDark
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      disabled={disabled || busy}
      onPress={onPress}
      style={({ pressed }) => [s.btn, { backgroundColor: bg, opacity: disabled ? 0.45 : pressed ? 0.88 : 1, transform: [{ scale: pressed ? 0.985 : 1 }] }, kind === 'primary' && shadow]}
    >
      {busy ? <ActivityIndicator color={fg} /> : <>{icon && <Ionicons name={icon} size={18} color={fg} />}<Text style={[s.btnText, { color: fg }]}>{label}</Text></>}
    </Pressable>
  )
}

/** A small selectable choice, used where several options sit side by side. */
export function Chip({ label, active, onPress }: { label: string; active?: boolean; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected: !!active }} onPress={onPress} style={{ borderRadius: 999, paddingHorizontal: 14, paddingVertical: 9, backgroundColor: active ? color.primary : color.white, borderWidth: 1.5, borderColor: active ? color.primary : color.line }}>
      <Text style={{ fontSize: 13, fontWeight: '700', color: active ? color.white : color.ink }}>{label}</Text>
    </Pressable>
  )
}

export function Field({ label, ...props }: TextInputProps & { label: string }) {
  const { align } = useI18n()
  return (
    <View style={{ gap: 6 }}>
      <Text style={[s.muted, { textAlign: align, fontWeight: '700', color: color.ink }]}>{label}</Text>
      <TextInput accessibilityLabel={label} placeholderTextColor={color.muted} style={[s.input, { textAlign: align }, props.multiline && { paddingTop: 14, textAlignVertical: 'top' }]} {...props} />
    </View>
  )
}

const tones = { ok: [color.okBg, color.ok], warn: [color.warnBg, color.warn], danger: [color.dangerBg, color.danger], info: [color.infoBg, color.info], neutral: [color.line, color.muted] } as const

export function Pill({ label, tone = 'neutral' }: { label: string; tone?: keyof typeof tones }) {
  return <View style={[s.pill, { backgroundColor: tones[tone][0] }]}><Text style={[s.pillText, { color: tones[tone][1] }]}>{label}</Text></View>
}

export function Banner({ text, tone = 'warn' }: { text: string; tone?: 'warn' | 'danger' | 'info' }) {
  const { align, row } = useI18n()
  const icon: IconName = tone === 'danger' ? 'alert-circle' : tone === 'info' ? 'information-circle' : 'warning'
  return (
    <View accessibilityRole="alert" style={{ flexDirection: row, alignItems: 'center', gap: 10, backgroundColor: tones[tone][0], borderRadius: 18, padding: 14 }}>
      <Ionicons name={icon} size={20} color={tones[tone][1]} />
      <Text style={[s.body, { flex: 1, color: tones[tone][1], textAlign: align, fontWeight: '600' }]}>{text}</Text>
    </View>
  )
}

export function Row({ children, gap = 8 }: { children: ReactNode; gap?: number }) {
  const { row } = useI18n()
  return <View style={{ flexDirection: row, alignItems: 'center', gap, flexWrap: 'wrap' }}>{children}</View>
}

/** The coloured welcome area at the top of the main screens. */
export function Hero({ title, subtitle, icon, children }: { title: string; subtitle?: string; icon?: IconName; children?: ReactNode }) {
  const { align, row } = useI18n()
  return (
    <View style={{ backgroundColor: color.primary, borderRadius: 28, padding: 20, gap: 14, overflow: 'hidden', ...shadow }}>
      <View style={{ position: 'absolute', top: -40, right: -30, width: 140, height: 140, borderRadius: 70, backgroundColor: 'rgba(255,255,255,0.10)' }} />
      <View style={{ position: 'absolute', bottom: -50, left: -20, width: 110, height: 110, borderRadius: 55, backgroundColor: 'rgba(245,181,68,0.22)' }} />
      <View style={{ flexDirection: row, alignItems: 'center', gap: 14 }}>
        {icon && <View style={{ width: 48, height: 48, borderRadius: 16, backgroundColor: 'rgba(255,255,255,0.18)', alignItems: 'center', justifyContent: 'center' }}><Ionicons name={icon} size={26} color={color.white} /></View>}
        <View style={{ flex: 1, gap: 2 }}>
          <Text accessibilityRole="header" style={{ fontSize: 24, fontWeight: '800', color: color.white, textAlign: align, letterSpacing: -0.3 }}>{title}</Text>
          {subtitle ? <Text style={{ fontSize: 13, color: 'rgba(255,255,255,0.85)', textAlign: align }}>{subtitle}</Text> : null}
        </View>
      </View>
      {children}
    </View>
  )
}

/** A number with a label and a coloured icon: the building block of the dashboards. */
export function Tile({ label, value, icon, tone = 'primary', alert }: { label: string; value: string | number; icon: IconName; tone?: Tone; alert?: boolean }) {
  const { align } = useI18n()
  return (
    <View style={[s.card, { flexBasis: '47%', flexGrow: 1, gap: 10 }]}>
      <IconChip name={icon} tone={alert ? 'accent' : tone} size={40} />
      <View style={{ gap: 2 }}>
        <Text style={{ fontSize: 28, fontWeight: '800', color: alert ? color.danger : color.ink, textAlign: align, letterSpacing: -0.5 }}>{String(value)}</Text>
        <Text style={[s.muted, { textAlign: align }]}>{label}</Text>
      </View>
    </View>
  )
}

export function ProgressBar({ value, max, tone = 'ok' }: { value: number; max: number; tone?: 'ok' | 'danger' }) {
  const pct = max <= 0 ? 0 : Math.max(0, Math.min(1, value / max))
  return (
    <View accessibilityRole="progressbar" style={{ height: 8, borderRadius: 4, backgroundColor: color.line, overflow: 'hidden' }}>
      <View style={{ width: `${pct * 100}%`, height: 8, borderRadius: 4, backgroundColor: tone === 'danger' ? color.accent : color.mint }} />
    </View>
  )
}

/** A friendly empty state, instead of a bare line of grey text. */
export function Empty({ icon = 'happy-outline', text }: { icon?: IconName; text: string }) {
  return (
    <View style={{ alignItems: 'center', gap: 10, paddingVertical: 28 }}>
      <IconChip name={icon} tone="sky" size={56} />
      <Text style={[s.muted, { textAlign: 'center', maxWidth: 260 }]}>{text}</Text>
    </View>
  )
}

/** A heading for a group of cards. */
export function Section({ children }: { children: ReactNode }) {
  const { align } = useI18n()
  return <Text accessibilityRole="header" style={{ fontSize: 13, fontWeight: '800', color: color.muted, textTransform: 'uppercase', letterSpacing: 0.8, textAlign: align, marginTop: 6 }}>{children}</Text>
}

export function Loading() {
  return <View style={{ padding: 24 }}><ActivityIndicator color={color.primary} /></View>
}
