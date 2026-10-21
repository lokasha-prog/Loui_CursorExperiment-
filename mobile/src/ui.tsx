import { type ReactNode } from 'react'
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View, type TextInputProps, type ViewStyle } from 'react-native'
import { useI18n } from './i18n'

export const color = { primary: '#0E7C86', primaryDark: '#0B5F67', accent: '#E07A5F', ink: '#0F172A', muted: '#64748B', line: '#E2E8F0', bg: '#F8FAFC', white: '#FFFFFF', danger: '#B91C1C', dangerBg: '#FEE2E2', ok: '#047857', okBg: '#D1FAE5', warn: '#B45309', warnBg: '#FEF3C7', info: '#1D4ED8', infoBg: '#DBEAFE' }

const s = StyleSheet.create({
  screen: { flex: 1, backgroundColor: color.bg },
  content: { padding: 16, gap: 12, paddingBottom: 32 },
  card: { backgroundColor: color.white, borderRadius: 14, borderWidth: 1, borderColor: color.line, padding: 14, gap: 6 },
  title: { fontSize: 22, fontWeight: '800', color: color.ink },
  heading: { fontSize: 16, fontWeight: '700', color: color.ink },
  body: { fontSize: 14, color: color.ink },
  muted: { fontSize: 12, color: color.muted },
  btn: { minHeight: 48, borderRadius: 12, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 18 },
  btnText: { fontSize: 15, fontWeight: '700' },
  input: { minHeight: 48, borderRadius: 12, borderWidth: 1, borderColor: color.line, backgroundColor: color.white, paddingHorizontal: 14, fontSize: 16, color: color.ink },
  pill: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: 10, paddingVertical: 3 },
  pillText: { fontSize: 11, fontWeight: '700' },
})

export function Screen({ children, refreshing, onRefresh }: { children: ReactNode; refreshing?: boolean; onRefresh?: () => void }) {
  return (
    <ScrollView
      style={s.screen}
      contentContainerStyle={s.content}
      keyboardShouldPersistTaps="handled"
      refreshControl={onRefresh ? <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} tintColor={color.primary} /> : undefined}
    >
      {children}
    </ScrollView>
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

export function Btn({ label, onPress, kind = 'primary', busy, disabled }: { label: string; onPress: () => void; kind?: 'primary' | 'secondary' | 'danger'; busy?: boolean; disabled?: boolean }) {
  const bg = kind === 'primary' ? color.primary : kind === 'danger' ? color.danger : color.white
  const fg = kind === 'secondary' ? color.primaryDark : color.white
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      disabled={disabled || busy}
      onPress={onPress}
      style={({ pressed }) => [s.btn, { backgroundColor: bg, opacity: disabled ? 0.5 : pressed ? 0.85 : 1 }, kind === 'secondary' && { borderWidth: 1, borderColor: color.primary }]}
    >
      {busy ? <ActivityIndicator color={fg} /> : <Text style={[s.btnText, { color: fg }]}>{label}</Text>}
    </Pressable>
  )
}

export function Field({ label, ...props }: TextInputProps & { label: string }) {
  const { align } = useI18n()
  return (
    <View style={{ gap: 4 }}>
      <Text style={[s.muted, { textAlign: align, fontWeight: '700' }]}>{label}</Text>
      <TextInput accessibilityLabel={label} placeholderTextColor={color.muted} style={[s.input, { textAlign: align }]} {...props} />
    </View>
  )
}

const tones = { ok: [color.okBg, color.ok], warn: [color.warnBg, color.warn], danger: [color.dangerBg, color.danger], info: [color.infoBg, color.info], neutral: [color.line, color.muted] } as const

export function Pill({ label, tone = 'neutral' }: { label: string; tone?: keyof typeof tones }) {
  return <View style={[s.pill, { backgroundColor: tones[tone][0] }]}><Text style={[s.pillText, { color: tones[tone][1] }]}>{label}</Text></View>
}

export function Banner({ text, tone = 'warn' }: { text: string; tone?: 'warn' | 'danger' | 'info' }) {
  const { align } = useI18n()
  return <View accessibilityRole="alert" style={[s.card, { backgroundColor: tones[tone][0], borderColor: tones[tone][1] }]}><Text style={[s.body, { color: tones[tone][1], textAlign: align, fontWeight: '600' }]}>{text}</Text></View>
}

export function Row({ children, gap = 8 }: { children: ReactNode; gap?: number }) {
  const { row } = useI18n()
  return <View style={{ flexDirection: row, alignItems: 'center', gap, flexWrap: 'wrap' }}>{children}</View>
}

export function Loading() {
  return <View style={{ padding: 24 }}><ActivityIndicator color={color.primary} /></View>
}
