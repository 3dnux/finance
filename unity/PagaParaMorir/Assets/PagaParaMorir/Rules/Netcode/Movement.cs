using System;
using System.Numerics;

namespace PagaParaMorir.Rules
{
    /// <summary>Input de un jugador en un tick de simulación.</summary>
    public struct PlayerInput
    {
        /// <summary>Número de tick del cliente (empieza en 1 y sube de a uno).</summary>
        public uint Tick;
        /// <summary>x = lateral, y = adelante. Largo máximo 1.</summary>
        public Vector2 Move;
        /// <summary>Hacia dónde mira, en grados (como Unity: 0 = +Z, 90 = +X).</summary>
        public float Yaw;
        public float Pitch;
        public bool Jump;
        public bool Fire;
        public bool Reload;
        public byte Weapon;
    }

    /// <summary>Estado de movimiento que se predice en el cliente y se corrige con el del servidor.</summary>
    public struct MoveState
    {
        public Vector3 Position;
        public float VerticalVelocity;
        public bool Grounded;

        public MoveState(Vector3 position, float verticalVelocity, bool grounded)
        {
            Position = position;
            VerticalVelocity = verticalVelocity;
            Grounded = grounded;
        }
    }

    /// <summary>
    /// Reglas de movimiento compartidas por cliente y servidor. Tienen que ser idénticas en ambos
    /// lados para que la predicción coincida: por eso viven aquí y no en el código de Unity.
    /// Las colisiones las resuelve quien aplica el desplazamiento (el CharacterController).
    /// </summary>
    public static class Movement
    {
        public const int TickRate = 30;
        public const float TickSeconds = 1f / TickRate;
        public const float MoveSpeed = 6.5f;
        public const float JumpSpeed = 7f;
        public const float Gravity = -20f;
        /// <summary>Pequeña velocidad hacia abajo en el suelo para que el CharacterController lo detecte.</summary>
        public const float GroundStick = -1f;

        /// <summary>
        /// Desplazamiento deseado de un tick. Actualiza la velocidad vertical en <paramref name="state"/>;
        /// la posición y si quedó en el suelo las pone quien aplica el desplazamiento.
        /// </summary>
        public static Vector3 Step(ref MoveState state, PlayerInput input, bool frozen, float dt = TickSeconds)
        {
            if (state.Grounded)
            {
                state.VerticalVelocity = GroundStick;
                if (input.Jump && !frozen) state.VerticalVelocity = JumpSpeed;
            }
            state.VerticalVelocity += Gravity * dt;

            var horizontal = frozen ? Vector3.Zero : Horizontal(input.Move, input.Yaw) * MoveSpeed;
            return new Vector3(horizontal.X * dt, state.VerticalVelocity * dt, horizontal.Z * dt);
        }

        /// <summary>Dirección en el plano según el input y hacia dónde mira (largo máximo 1).</summary>
        public static Vector3 Horizontal(Vector2 move, float yawDegrees)
        {
            if (move.LengthSquared() > 1f) move = Vector2.Normalize(move);
            var rad = yawDegrees * (float)Math.PI / 180f;
            var sin = (float)Math.Sin(rad);
            var cos = (float)Math.Cos(rad);
            // Igual que Quaternion.Euler(0, yaw, 0) * (x, 0, z) en Unity.
            return new Vector3(move.X * cos + move.Y * sin, 0, -move.X * sin + move.Y * cos);
        }

        /// <summary>
        /// Simulación sin obstáculos (piso plano en y = <paramref name="floorY"/>).
        /// Sirve para pruebas y como referencia del comportamiento esperado.
        /// </summary>
        public static MoveState SimulateFlat(MoveState state, PlayerInput input, bool frozen, float floorY = 0f)
        {
            var delta = Step(ref state, input, frozen);
            var next = state.Position + delta;
            state.Grounded = next.Y <= floorY;
            if (state.Grounded) next.Y = floorY;
            state.Position = next;
            return state;
        }
    }
}
