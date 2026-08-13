import * as Haptics from 'expo-haptics';
import React, { useEffect, useMemo, useRef } from 'react';
import { Animated, Easing, Modal, Platform, Pressable, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { isAngryMood, isHappyMood, phraseFor, type CatMood } from '@/lib/mascot';
import { formatMoney } from '@/lib/money';
import { radius, spacing, useTheme } from '@/theme';

import { CatFace } from './CatFace';
import { AppText } from './ui';

const HEART = 'M12 21 C4 14 2 9 6 5.5 C9 3 12 6 12 8 C12 6 15 3 18 5.5 C22 9 20 14 12 21 Z';
/** Los corazones van siempre en rosa: en verde no se leen como corazones. */
const HEART_COLOR = '#E7415B';

/** Corazoncitos o nubes de vapor que suben alrededor del gatito. */
function Particle({
  index,
  angry,
  color,
  play,
}: {
  index: number;
  angry: boolean;
  color: string;
  play: boolean;
}) {
  const progress = useRef(new Animated.Value(0)).current;
  const drift = useMemo(() => [-58, -30, 34, 62][index % 4], [index]);
  const scale = useMemo(() => [0.9, 1.15, 0.8, 1][index % 4], [index]);

  useEffect(() => {
    if (!play) return;
    const animation = Animated.loop(
      Animated.sequence([
        Animated.delay(index * 190),
        Animated.timing(progress, {
          toValue: 1,
          duration: 1500,
          easing: Easing.out(Easing.quad),
          useNativeDriver: true,
        }),
        Animated.timing(progress, { toValue: 0, duration: 0, useNativeDriver: true }),
      ]),
    );
    animation.start();
    return () => animation.stop();
  }, [play, index, progress]);

  return (
    <Animated.View
      pointerEvents="none"
      style={{
        position: 'absolute',
        opacity: progress.interpolate({ inputRange: [0, 0.15, 0.75, 1], outputRange: [0, 1, 1, 0] }),
        transform: [
          { translateX: drift },
          { translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [30, -90] }) },
          { scale: progress.interpolate({ inputRange: [0, 1], outputRange: [0.4 * scale, 1.15 * scale] }) },
        ],
      }}
    >
      {angry ? (
        // Nubecilla de vapor saliendo de la cabeza.
        <View style={{ width: 22, height: 22, borderRadius: 11, backgroundColor: color, opacity: 0.4 }} />
      ) : (
        <Svg width={24} height={24} viewBox="0 0 24 24">
          <Path d={HEART} fill={HEART_COLOR} />
        </Svg>
      )}
    </Animated.View>
  );
}

/**
 * Pantalla breve con la reacción del gatito tras registrar un movimiento.
 * Se cierra sola; tocar la pantalla la cierra antes.
 */
export function CatReaction({
  visible,
  mood,
  amount,
  currency,
  onDone,
  duration = 1600,
}: {
  visible: boolean;
  mood: CatMood;
  amount: number;
  currency: string;
  onDone: () => void;
  duration?: number;
}) {
  const { colors } = useTheme();
  const enter = useRef(new Animated.Value(0)).current;
  // El callback vive en una ref para que volver a renderizar no reinicie el temporizador.
  const doneRef = useRef(onDone);
  doneRef.current = onDone;
  // La frase se fija al abrir, para que no cambie a mitad de animación.
  const phrase = useMemo(() => (visible ? phraseFor(mood, Math.floor(Math.random() * 4)) : ''), [visible, mood]);

  const happy = isHappyMood(mood);
  const angry = isAngryMood(mood);
  const accent = happy ? colors.income : angry ? colors.expense : colors.transfer;

  useEffect(() => {
    if (!visible) {
      enter.setValue(0);
      return;
    }

    if (Platform.OS !== 'web') {
      void Haptics.notificationAsync(
        happy ? Haptics.NotificationFeedbackType.Success : angry ? Haptics.NotificationFeedbackType.Warning : Haptics.NotificationFeedbackType.Success,
      );
    }

    Animated.spring(enter, { toValue: 1, damping: 11, stiffness: 160, useNativeDriver: true }).start();
    const timer = setTimeout(() => doneRef.current(), duration);
    return () => clearTimeout(timer);
  }, [visible, happy, angry, duration, enter]);

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onDone} statusBarTranslucent>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Continuar"
        onPress={() => doneRef.current()}
        style={{
          flex: 1,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: colors.overlay,
          padding: spacing.xl,
        }}
      >
        <Animated.View
          style={{
            alignItems: 'center',
            gap: spacing.md,
            paddingVertical: spacing.xl,
            paddingHorizontal: spacing.xxl,
            borderRadius: radius.xl,
            backgroundColor: colors.surface,
            borderWidth: 2,
            borderColor: accent,
            opacity: enter,
            transform: [
              { scale: enter.interpolate({ inputRange: [0, 1], outputRange: [0.7, 1] }) },
            ],
          }}
        >
          <View style={{ alignItems: 'center', justifyContent: 'center' }}>
            {[0, 1, 2, 3].map((index) => (
              <Particle
                key={index}
                index={index}
                angry={angry}
                color={colors.textMuted}
                play={visible && mood !== 'neutral'}
              />
            ))}
            <CatFace mood={mood} size={140} />
          </View>

          <AppText variant="title" color={accent} style={{ textAlign: 'center' }}>
            {phrase}
          </AppText>
          <AppText variant="subtitle" weight="800">
            {formatMoney(amount, currency, { signed: happy })}
          </AppText>
        </Animated.View>
      </Pressable>
    </Modal>
  );
}
