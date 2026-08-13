import React, { useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Animated, Easing, View } from 'react-native';
import Svg, { Circle, Ellipse, G, Path } from 'react-native-svg';

import type { CatMood } from '@/lib/mascot';

/**
 * Paleta del gatito. No depende del tema: es una mascota, y mantener su color
 * en claro y oscuro hace que se reconozca siempre igual.
 */
const FUR = '#F2A65A';
const FUR_DARK = '#E08C3C';
const INK = '#3A2A22';
const INNER_EAR = '#F6C2CE';
const BLUSH = '#F2748E';
const NOSE = '#E9899B';
const EYE = '#F7E7A3';
const HEART = '#E7415B';
const ANGER = '#E23D3D';

/** Corazón centrado en (cx, cy) con «radio» s, para los ojos enamorados. */
function heartPath(cx: number, cy: number, s: number): string {
  return [
    `M ${cx} ${cy + s}`,
    `C ${cx - 1.5 * s} ${cy - 0.2 * s} ${cx - 1.1 * s} ${cy - 1.4 * s} ${cx} ${cy - 0.55 * s}`,
    `C ${cx + 1.1 * s} ${cy - 1.4 * s} ${cx + 1.5 * s} ${cy - 0.2 * s} ${cx} ${cy + s}`,
    'Z',
  ].join(' ');
}

const EYE_LEFT = { x: 37, y: 52 };
const EYE_RIGHT = { x: 63, y: 52 };

function Eyes({ mood, blinking }: { mood: CatMood; blinking: boolean }) {
  // Ojos cerrados en arco: es el gesto del parpadeo y también la sonrisa feliz.
  if (blinking || mood === 'happy') {
    return (
      <G stroke={INK} strokeWidth={4.5} strokeLinecap="round" fill="none">
        <Path d="M30 55 Q37 45 44 55" />
        <Path d="M56 55 Q63 45 70 55" />
      </G>
    );
  }

  if (mood === 'love') {
    return (
      <G fill={HEART}>
        <Path d={heartPath(EYE_LEFT.x, EYE_LEFT.y - 1, 7)} />
        <Path d={heartPath(EYE_RIGHT.x, EYE_RIGHT.y - 1, 7)} />
      </G>
    );
  }

  const angry = mood === 'angry' || mood === 'furious';
  // Enfadado: ojos más pequeños, pupila más cerrada y cejas caídas hacia el centro.
  const radius = angry ? 5.6 : 6.8;
  const pupilRy = angry ? 4.2 : 5.2;
  const pupilRx = angry ? 1.2 : 1.9;

  return (
    <G>
      <Circle cx={EYE_LEFT.x} cy={EYE_LEFT.y} r={radius} fill={EYE} stroke={INK} strokeWidth={1.6} />
      <Circle cx={EYE_RIGHT.x} cy={EYE_RIGHT.y} r={radius} fill={EYE} stroke={INK} strokeWidth={1.6} />
      <Ellipse cx={EYE_LEFT.x} cy={EYE_LEFT.y} rx={pupilRx} ry={pupilRy} fill={INK} />
      <Ellipse cx={EYE_RIGHT.x} cy={EYE_RIGHT.y} rx={pupilRx} ry={pupilRy} fill={INK} />
      {!angry ? (
        <G fill="#FFFFFF" opacity={0.9}>
          <Circle cx={EYE_LEFT.x + 2.4} cy={EYE_LEFT.y - 2.6} r={1.5} />
          <Circle cx={EYE_RIGHT.x + 2.4} cy={EYE_RIGHT.y - 2.6} r={1.5} />
        </G>
      ) : null}
      {angry ? (
        <G stroke={INK} strokeWidth={4} strokeLinecap="round">
          <Path d={mood === 'furious' ? 'M27 38 L46 47' : 'M28 41 L45 47'} />
          <Path d={mood === 'furious' ? 'M73 38 L54 47' : 'M72 41 L55 47'} />
        </G>
      ) : null}
    </G>
  );
}

function Mouth({ mood }: { mood: CatMood }) {
  if (mood === 'furious') {
    // Boca abierta con colmillos: el bufido.
    return (
      <G>
        <Path d="M39 66 Q50 61 61 66 Q58 81 50 81 Q42 81 39 66 Z" fill={INK} />
        <Path d="M43 66 L46 72 L49 66 Z" fill="#FFFFFF" />
        <Path d="M51 66 L54 72 L57 66 Z" fill="#FFFFFF" />
        <Path d="M46 76 Q50 73 54 76 Q52 80 50 80 Q48 80 46 76 Z" fill={BLUSH} opacity={0.9} />
      </G>
    );
  }

  if (mood === 'angry') {
    return <Path d="M41 73 Q50 64 59 73" stroke={INK} strokeWidth={3.4} strokeLinecap="round" fill="none" />;
  }

  if (mood === 'neutral') {
    return (
      <Path d="M44 67 Q47 70.5 50 67 Q53 70.5 56 67" stroke={INK} strokeWidth={3} strokeLinecap="round" fill="none" />
    );
  }

  // Boca de gato contenta: la clásica «ω».
  return (
    <Path d="M40 66 Q45.5 73 50 67 Q54.5 73 60 66" stroke={INK} strokeWidth={3.4} strokeLinecap="round" fill="none" />
  );
}

export function CatFace({
  mood,
  size = 96,
  animate = true,
}: {
  mood: CatMood;
  size?: number;
  animate?: boolean;
}) {
  const [blinking, setBlinking] = useState(false);
  const [reduceMotion, setReduceMotion] = useState(false);
  const wiggle = useRef(new Animated.Value(0)).current;
  const bob = useRef(new Animated.Value(0)).current;

  const happy = mood === 'happy' || mood === 'love';
  const angry = mood === 'angry' || mood === 'furious';
  const motion = animate && !reduceMotion;

  useEffect(() => {
    let active = true;
    AccessibilityInfo.isReduceMotionEnabled().then((enabled) => {
      if (active) setReduceMotion(enabled);
    });
    const listener = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduceMotion);
    return () => {
      active = false;
      listener.remove();
    };
  }, []);

  // Parpadeo espontáneo: solo cuando los ojos están abiertos.
  useEffect(() => {
    if (!motion || happy) return;
    let timeout: ReturnType<typeof setTimeout>;
    const schedule = () => {
      timeout = setTimeout(
        () => {
          setBlinking(true);
          setTimeout(() => {
            setBlinking(false);
            schedule();
          }, 130);
        },
        2200 + Math.random() * 2600,
      );
    };
    schedule();
    return () => clearTimeout(timeout);
  }, [motion, happy]);

  // Gesto continuo: el contento se balancea, el enfadado tiembla de rabia.
  useEffect(() => {
    wiggle.setValue(0);
    bob.setValue(0);
    if (!motion) return;

    const swing = (value: Animated.Value, duration: number) =>
      Animated.loop(
        Animated.sequence([
          Animated.timing(value, { toValue: 1, duration, easing: Easing.inOut(Easing.quad), useNativeDriver: true }),
          Animated.timing(value, { toValue: -1, duration, easing: Easing.inOut(Easing.quad), useNativeDriver: true }),
        ]),
      );

    const animation = angry
      ? swing(wiggle, mood === 'furious' ? 55 : 90)
      : happy
        ? Animated.parallel([swing(wiggle, 700), swing(bob, 520)])
        : swing(bob, 1600);

    animation.start();
    return () => animation.stop();
  }, [motion, mood, angry, happy, wiggle, bob]);

  const rotate = wiggle.interpolate({
    inputRange: [-1, 1],
    outputRange: angry ? ['-3deg', '3deg'] : ['-7deg', '7deg'],
  });
  const translateX = wiggle.interpolate({
    inputRange: [-1, 1],
    outputRange: angry ? [-size * 0.035, size * 0.035] : [0, 0],
  });
  const translateY = bob.interpolate({
    inputRange: [-1, 1],
    outputRange: [size * 0.02, -size * 0.02],
  });

  return (
    <Animated.View
      accessible
      accessibilityRole="image"
      accessibilityLabel={
        happy ? 'Gatito contento' : angry ? 'Gatito enfadado' : 'Gatito tranquilo'
      }
      style={{ width: size, height: size, transform: [{ translateX }, { translateY }, { rotate }] }}
    >
      <Svg width={size} height={size} viewBox="0 0 100 100">
        {/* Orejas, detrás de la cabeza */}
        <G stroke={INK} strokeWidth={2.4} strokeLinejoin="round">
          <Path d="M21 40 L25 9 L47 26 Z" fill={FUR} />
          <Path d="M79 40 L75 9 L53 26 Z" fill={FUR} />
        </G>
        <G>
          <Path d="M26 34 L28.5 18 L40 27 Z" fill={INNER_EAR} />
          <Path d="M74 34 L71.5 18 L60 27 Z" fill={INNER_EAR} />
        </G>

        {/* Cabeza */}
        <Ellipse cx={50} cy={56} rx={34} ry={30} fill={FUR} stroke={INK} strokeWidth={2.4} />
        {/* Mancha de la frente, para que no sea una bola lisa */}
        <Path d="M50 26 Q56 34 50 40 Q44 34 50 26 Z" fill={FUR_DARK} opacity={0.55} />

        {/* Bigotes */}
        <G stroke={INK} strokeWidth={1.8} strokeLinecap="round" opacity={0.75}>
          <Path d="M6 56 L26 58" />
          <Path d="M5 64 L26 63" />
          <Path d="M7 72 L27 68" />
          <Path d="M94 56 L74 58" />
          <Path d="M95 64 L74 63" />
          <Path d="M93 72 L73 68" />
        </G>

        {happy ? (
          <G fill={BLUSH} opacity={0.45}>
            <Ellipse cx={25} cy={64} rx={7.5} ry={4.6} />
            <Ellipse cx={75} cy={64} rx={7.5} ry={4.6} />
          </G>
        ) : null}

        <Eyes mood={mood} blinking={blinking} />

        {/* Nariz */}
        <Path d="M45.5 60 L54.5 60 L50 65.5 Z" fill={NOSE} stroke={INK} strokeWidth={1.2} strokeLinejoin="round" />

        <Mouth mood={mood} />

        {/* Marca de enfado, fuera de la cabeza para no pisar la oreja ni los bigotes */}
        {angry ? (
          <G stroke={ANGER} strokeWidth={3.2} strokeLinecap="round">
            <Path d="M85 30 L95 40" />
            <Path d="M95 30 L85 40" />
          </G>
        ) : null}
      </Svg>
    </Animated.View>
  );
}

/** Versión estática y pequeña, para listas o cabeceras. */
export function CatBadge({ mood, size = 40 }: { mood: CatMood; size?: number }) {
  return (
    <View style={{ width: size, height: size }}>
      <CatFace mood={mood} size={size} animate={false} />
    </View>
  );
}
