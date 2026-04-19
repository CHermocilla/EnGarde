using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

public class Player
{
    public enum PlayerState
    {
        Idle,
        Walk,
        Run,
        Attack1,
        Attack2,
        Attack3,
        RunAttack,
        Defend,
        Hurt,
        Recovery,
        Stunned,
        GuardBroken
    }

    public Vector2 Position;
    private Dictionary<string, Texture2D> _animations;
    private Texture2D _activeTex;
    public PlayerState _currentState = PlayerState.Idle;
    private SpriteEffects _facing = SpriteEffects.None;
    private float _knightScale = 3.5f;

    private int _currentFrame = 0;
    private float _timer = 0f;
    private float _interval = 0.08f; // faster pacing

    private float _attackCooldownTimer = 0f;

    private float _dashTimer = 0f;
    private float _dashTimeRemaining = 0f;
    private float _dashDirection = 0f;
    private const float _dashDuration = 0.18f;
    private const float _doubleTapThreshold = 0.25f;

    private Keys _lastPressedKey = Keys.None;
    private KeyboardState _prevKState;

    public string Label = "P1";
    public Color KnightTint = Color.White;
    public int Health = 3;
    public bool IsDead => Health <= 0;

    public Rectangle Hurtbox;
    public Rectangle Hitbox;
    public Rectangle Pushbox;

    private bool _hasConnectedThisAttack = false;
    private bool _landedCleanHit = false;

    // Guard system
    public float BlockStamina = 100f;
    public float MaxBlockStamina = 100f;

    private float _guardBreakTimer = 0f;
    private const float _guardBreakDuration = 0.9f;
    private const float _blockRegenRate = 26f;
    private const float _blockDamageLight = 12f;
    private const float _blockDamageHeavy = 22f;
    private const float _blockDamageFinisher = 30f;

    // Punish / finisher system
    private float _punishFollowupTimer = 0f;
    private const float _punishFollowupDuration = 0.9f;
    private bool _canUseFinisher = false;

    private bool _justTriggeredPunish = false;

    private bool _justLandedCleanHit = false;

    private bool _justBlockedHit = false;

    private PlayerControls _controls;

    public bool JustBlockedHit()
    {
        return _justBlockedHit;
    }

    public Player(Dictionary<string, Texture2D> animationFiles, Vector2 startPos, PlayerControls controls)
    {
        _animations = animationFiles;
        _activeTex = _animations["idle"];
        Position = startPos;
        _controls = controls;
    }

    public struct PlayerControls
    {
        public Keys Left, Right, Attack1, Attack2;
    }

    private Dictionary<PlayerState, float> _animationOffsets = new Dictionary<PlayerState, float>()
    {
        { PlayerState.Idle, 52f },
        { PlayerState.Walk, 34f },
        { PlayerState.Run, 54f },
        { PlayerState.Attack1, 52f },
        { PlayerState.Attack2, 52f },
        { PlayerState.Attack3, 52f },
        { PlayerState.RunAttack, 52f },
        { PlayerState.Defend, 52f },
        { PlayerState.Hurt, 52f },
        { PlayerState.Recovery, 52f },
        { PlayerState.Stunned, 52f },
        { PlayerState.GuardBroken, 52f }
    };

    private bool IsAttackState(PlayerState state)
    {
        return state == PlayerState.Attack1 ||
               state == PlayerState.Attack2 ||
               state == PlayerState.Attack3 ||
               state == PlayerState.RunAttack;
    }

    private void StartAttack(PlayerState attackType, float cooldown)
    {
        _currentState = attackType;
        _currentFrame = 0;
        _timer = 0f;
        _attackCooldownTimer = cooldown;
        _hasConnectedThisAttack = false;
        _landedCleanHit = false;
        Hitbox = Rectangle.Empty;
    }

    private void StartBackDash()
    {
        _dashTimeRemaining = _dashDuration;
        _dashDirection = (_facing == SpriteEffects.None) ? -1f : 1f;
    }

    public void Update(GameTime gameTime, int screenWidth, Player opponent)
    {
        KeyboardState kstate = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        Hitbox = Rectangle.Empty;
        _justTriggeredPunish = false;
        _justLandedCleanHit = false;
        _justBlockedHit = false;

        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= dt;

        if (_dashTimer < _doubleTapThreshold)
            _dashTimer += dt;

        if (_punishFollowupTimer > 0f)
        {
            _punishFollowupTimer -= dt;
            if (_punishFollowupTimer <= 0f)
            {
                _punishFollowupTimer = 0f;
                _canUseFinisher = false;
            }
        }

        if (_guardBreakTimer > 0f)
        {
            _guardBreakTimer -= dt;

            if (_currentState != PlayerState.Hurt &&
                _currentState != PlayerState.Stunned &&
                !IsAttackState(_currentState))
            {
                _currentState = PlayerState.GuardBroken;
            }

            if (_guardBreakTimer <= 0f)
            {
                _guardBreakTimer = 0f;

                if (_currentState == PlayerState.GuardBroken)
                    _currentState = PlayerState.Idle;
            }
        }
        else
        {
            if (_currentState != PlayerState.Defend &&
                !IsAttackState(_currentState) &&
                _currentState != PlayerState.Hurt &&
                _currentState != PlayerState.Recovery &&
                _currentState != PlayerState.Stunned &&
                _currentState != PlayerState.GuardBroken)
            {
                BlockStamina += _blockRegenRate * dt;
                if (BlockStamina > MaxBlockStamina)
                    BlockStamina = MaxBlockStamina;
            }
        }

        bool isAttacking = IsAttackState(_currentState);
        bool isHurt = _currentState == PlayerState.Hurt;
        bool isRecovering = _currentState == PlayerState.Recovery;
        bool isStunned = _currentState == PlayerState.Stunned;
        bool isGuardBroken = _currentState == PlayerState.GuardBroken;

        if (Label == "CPU")
        {
            UpdateCPULogic(dt, opponent);
        }
        else if (!isAttacking && !isHurt && !isRecovering && !isStunned && !isGuardBroken)
        {
            _facing = (opponent.Position.X > Position.X) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            if (_dashTimeRemaining > 0f)
            {
                Position.X += _dashDirection * 850f * dt;
                _dashTimeRemaining -= dt;
                _currentState = PlayerState.Run;
            }
            else
            {
                bool holdingRight = kstate.IsKeyDown(_controls.Right);
                bool holdingLeft = kstate.IsKeyDown(_controls.Left);

                bool isMovingBackwards =
                    (_facing == SpriteEffects.None && holdingLeft) ||
                    (_facing == SpriteEffects.FlipHorizontally && holdingRight);

                bool isMovingForwards =
                    (_facing == SpriteEffects.None && holdingRight) ||
                    (_facing == SpriteEffects.FlipHorizontally && holdingLeft);

                if (isMovingBackwards)
                {
                    _currentState = PlayerState.Defend;

                    Keys backKey = (_facing == SpriteEffects.None) ? _controls.Left : _controls.Right;
                    if (JustPressed(kstate, backKey))
                    {
                        if (_lastPressedKey == backKey && _dashTimer < _doubleTapThreshold)
                            StartBackDash();

                        _lastPressedKey = backKey;
                        _dashTimer = 0f;
                    }
                }
                else if (isMovingForwards)
                {
                    _currentState = PlayerState.Walk;
                    Position.X += (_facing == SpriteEffects.None ? 320f : -320f) * dt;

                    Keys forwardKey = (_facing == SpriteEffects.None) ? _controls.Right : _controls.Left;
                    if (JustPressed(kstate, forwardKey))
                    {
                        _lastPressedKey = forwardKey;
                        _dashTimer = 0f;
                    }
                }
                else
                {
                    _currentState = PlayerState.Idle;
                }

                if (_attackCooldownTimer <= 0f)
                {
                    if (_canUseFinisher && JustPressed(kstate, _controls.Attack2))
                    {
                        StartAttack(PlayerState.Attack3, 0.4f);
                        _canUseFinisher = false;
                        _punishFollowupTimer = 0f;
                    }
                    else if (JustPressed(kstate, _controls.Attack1))
                    {
                        StartAttack(PlayerState.Attack1, 0.22f);
                    }
                    else if (JustPressed(kstate, _controls.Attack2))
                    {
                        StartAttack(PlayerState.Attack2, 0.25f);
                    }
                }
            }
        }

        KeepInBounds(screenWidth);
        UpdateBoxes();
        HandleCombatCollision(opponent);
        UpdateAnimation(dt);
        _prevKState = kstate;
    }

    private void UpdateBoxes()
    {
        if (_currentState == PlayerState.GuardBroken)
            Hurtbox = new Rectangle((int)Position.X - 60, (int)Position.Y - 280, 120, 280);
        else
            Hurtbox = new Rectangle((int)Position.X - 50, (int)Position.Y - 280, 100, 280);

        Pushbox = new Rectangle((int)Position.X - 40, (int)Position.Y - 180, 80, 180);
    }

    public void ResolvePlayerCollision(Player opponent)
    {
        if (!Pushbox.Intersects(opponent.Pushbox))
            return;

        Rectangle overlap = Rectangle.Intersect(Pushbox, opponent.Pushbox);

        if (overlap.Width <= 0)
            return;

        float pushAmount = overlap.Width / 2f;

        if (Position.X < opponent.Position.X)
        {
            Position.X -= pushAmount;
            opponent.Position.X += pushAmount;
        }
        else
        {
            Position.X += pushAmount;
            opponent.Position.X -= pushAmount;
        }

        UpdateBoxes();
        opponent.UpdateBoxes();
    }

    public bool TakeHit(float attackerX, float blockDamage = 18f, int damage = 1)
    {
        if (_currentState == PlayerState.Defend && _guardBreakTimer <= 0f)
        {
            BlockStamina -= blockDamage;

            if (BlockStamina <= 0f)
            {
                BlockStamina = 0f;
                _currentState = PlayerState.GuardBroken;
                _guardBreakTimer = _guardBreakDuration;
                _currentFrame = 0;
                _timer = 0f;

                Position.X += (Position.X > attackerX) ? 8f : -8f;
                UpdateBoxes();
                return true;
            }

            _justBlockedHit = true;
            Position.X += (Position.X > attackerX) ? 8f : -8f;
            UpdateBoxes();
            return false;
        }

        if (_currentState == PlayerState.Recovery || _currentState == PlayerState.GuardBroken)
        {
            _currentState = PlayerState.Stunned;
            Health -= damage;
        }
        else
        {
            _currentState = PlayerState.Hurt;
            Health -= damage;
        }

        if (Health < 0)
            Health = 0;

        _currentFrame = 0;
        _timer = 0f;

        Position.X += (Position.X > attackerX) ? 8f : -8f;
        UpdateBoxes();
        return true;
    }

    private void UpdateAnimation(float dt)
    {
        _timer += dt;

        if (_timer > _interval)
        {
            _currentFrame++;
            _timer = 0f;

            if (_currentFrame >= GetTotalFrames(_currentState))
            {
                if (IsAttackState(_currentState))
                {
                    _currentState = PlayerState.Recovery;
                }
                else if (_currentState == PlayerState.Hurt ||
                         _currentState == PlayerState.Recovery ||
                         _currentState == PlayerState.Stunned)
                {
                    _currentState = PlayerState.Idle;
                }
                else if (_currentState == PlayerState.GuardBroken && _guardBreakTimer <= 0f)
                {
                    _currentState = PlayerState.Idle;
                }

                _currentFrame = 0;
            }
        }

        string stateKey = _currentState.ToString().ToLower();

        if (stateKey == "stunned" || stateKey == "guardbroken")
            stateKey = "hurt";

        if (stateKey == "recovery")
            stateKey = "idle";

        if (_animations.ContainsKey(stateKey))
            _activeTex = _animations[stateKey];
        else
            _activeTex = _animations["idle"];
    }

    private int GetTotalFrames(PlayerState state)
    {
        return state switch
        {
            PlayerState.Idle => 4,
            PlayerState.Walk => 8,
            PlayerState.Run => 7,
            PlayerState.Attack1 => 4,
            PlayerState.Attack2 => 5,
            PlayerState.Attack3 => 4,
            PlayerState.RunAttack => 4,
            PlayerState.Defend => 5,
            PlayerState.Hurt => 2,
            PlayerState.Recovery => 3,
            PlayerState.Stunned => 10,
            PlayerState.GuardBroken => 8,
            _ => 1
        };
    }

    private void HandleCombatCollision(Player opponent)
    {
        if (!IsAttackState(_currentState))
            return;

        int activeStart;
        int activeEnd;

        switch (_currentState)
        {
            case PlayerState.Attack1:
                activeStart = 1;
                activeEnd = 2;
                break;
            case PlayerState.Attack2:
                activeStart = 2;
                activeEnd = 3;
                break;
            case PlayerState.Attack3:
                activeStart = 2;
                activeEnd = 4;
                break;
            default:
                activeStart = 2;
                activeEnd = 3;
                break;
        }

        if (_currentFrame < activeStart || _currentFrame > activeEnd)
            return;

        int attackWidth;
        int attackHeight;
        int xOffset;
        int yOffset = (int)(60 * _knightScale);
        float blockDamage;
        int damage;

        switch (_currentState)
        {
            case PlayerState.Attack1:
                attackWidth = (int)(22 * _knightScale);
                attackHeight = (int)(14 * _knightScale);
                xOffset = (_facing == SpriteEffects.None)
                    ? (int)(8 * _knightScale)
                    : (int)(-8 * _knightScale) - attackWidth;
                blockDamage = _blockDamageLight;
                damage = 1;
                break;

            case PlayerState.Attack2:
                attackWidth = (int)(34 * _knightScale);
                attackHeight = (int)(18 * _knightScale);
                xOffset = (_facing == SpriteEffects.None)
                    ? (int)(12 * _knightScale)
                    : (int)(-12 * _knightScale) - attackWidth;
                blockDamage = _blockDamageHeavy;
                damage = 1;
                break;

            case PlayerState.Attack3:
                attackWidth = (int)(40 * _knightScale);
                attackHeight = (int)(20 * _knightScale);
                xOffset = (_facing == SpriteEffects.None)
                    ? (int)(16 * _knightScale)
                    : (int)(-16 * _knightScale) - attackWidth;
                blockDamage = _blockDamageFinisher;
                damage = 2;
                break;

            default:
                attackWidth = (int)(25 * _knightScale);
                attackHeight = (int)(15 * _knightScale);
                xOffset = (_facing == SpriteEffects.None)
                    ? (int)(10 * _knightScale)
                    : (int)(-10 * _knightScale) - attackWidth;
                blockDamage = _blockDamageLight;
                damage = 1;
                break;
        }

        Hitbox = new Rectangle(
            (int)Position.X + xOffset,
            (int)Position.Y - yOffset,
            attackWidth,
            attackHeight
        );

        if (_hasConnectedThisAttack)
            return;

        if (!Hitbox.Intersects(opponent.Hurtbox))
            return;

        bool opponentWasVulnerable =
            opponent._currentState == PlayerState.Recovery ||
            opponent._currentState == PlayerState.Stunned ||
            opponent._currentState == PlayerState.GuardBroken;

        bool hitConnected = opponent.TakeHit(Position.X, blockDamage, damage);

        _hasConnectedThisAttack = true;

        if (hitConnected)
        {
            _landedCleanHit = true;
            _justLandedCleanHit = true;

            if (_currentState == PlayerState.Attack2 && opponentWasVulnerable)
            {
                _canUseFinisher = true;
                _punishFollowupTimer = _punishFollowupDuration;
                _justTriggeredPunish = true;
            }
        }
    }

    private void KeepInBounds(int screenWidth)
    {
        float margin = 32f * _knightScale;

        if (Position.X < margin)
            Position.X = margin;

        if (Position.X > screenWidth - margin)
            Position.X = screenWidth - margin;
    }

    private bool JustPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && _prevKState.IsKeyUp(key);
    }

    private Vector2 GetCurrentOrigin()
    {
        float xOrigin = _animationOffsets.ContainsKey(_currentState)
            ? _animationOffsets[_currentState]
            : 64f;

        return new Vector2(xOrigin, 128);
    }

    public bool JustTriggeredPunish()
    {
        return _justTriggeredPunish;
    }

    public bool JustDealtDamage()
    {
        return _justLandedCleanHit;
    }

    public bool IsUsingFinisher()
    {
        return _currentState == PlayerState.Attack3;
    }

    private void UpdateCPULogic(float dt, Player opponent)
    {
        if (Math.Abs(opponent.Position.X - Position.X) > 20f)
            _facing = (opponent.Position.X > Position.X) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        float distance = Math.Abs(opponent.Position.X - Position.X);

        bool opponentAttacking = IsAttackState(opponent._currentState);
        bool opponentVulnerable =
            opponent._currentState == PlayerState.Recovery ||
            opponent._currentState == PlayerState.Stunned ||
            opponent._currentState == PlayerState.GuardBroken;

        bool selfBusy =
            IsAttackState(_currentState) ||
            _currentState == PlayerState.Hurt ||
            _currentState == PlayerState.Recovery ||
            _currentState == PlayerState.Stunned ||
            _currentState == PlayerState.GuardBroken;

        if (_guardBreakTimer > 0f || selfBusy)
            return;

        // Take finisher if available
        if (_canUseFinisher && _attackCooldownTimer <= 0f)
        {
            StartAttack(PlayerState.Attack3, 0.4f);
            _canUseFinisher = false;
            _punishFollowupTimer = 0f;
            return;
        }

        // Punish vulnerable opponent with heavy
        if (opponentVulnerable && distance < 140f && _attackCooldownTimer <= 0f)
        {
            StartAttack(PlayerState.Attack2, 0.25f);
            return;
        }

        // Block if opponent is attacking and close enough
        if (opponentAttacking && distance < 130f && BlockStamina > 15f)
        {
            _currentState = PlayerState.Defend;
            return;
        }

        // Light poke range
        if (distance >= 80f && distance <= 110f && _attackCooldownTimer <= 0f)
        {
            StartAttack(PlayerState.Attack1, 0.22f);
            return;
        }

        // Heavy range occasionally
        if (distance > 110f && distance <= 145f && _attackCooldownTimer <= 0f)
        {
            StartAttack(PlayerState.Attack2, 0.25f);
            return;
        }

        // Walk in if too far
        if (distance > 120f)
        {
            _currentState = PlayerState.Walk;
            Position.X += (_facing == SpriteEffects.None ? 1 : -1) * 190f * dt;
            return;


            // Back off a little if too close and low guard
            if (distance < 70f && BlockStamina < 35f)
            {
                _currentState = PlayerState.Defend;
                Position.X += (_facing == SpriteEffects.None ? -1 : 1) * 90f * dt;
                return;
            }

            // Default idle / hold ground
            _currentState = PlayerState.Idle;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        int frameWidth = 128;
        int frameHeight = 128;

        int maxFrames = _activeTex.Width / frameWidth;
        int safeFrame = _currentFrame;

        if (maxFrames <= 0)
            return;

        if (safeFrame < 0)
            safeFrame = 0;

        if (safeFrame >= maxFrames)
            safeFrame = maxFrames - 1;

        Rectangle sourceRect = new Rectangle(safeFrame * frameWidth, 0, frameWidth, frameHeight);
        Vector2 origin = GetCurrentOrigin();

        if (_facing == SpriteEffects.FlipHorizontally)
            origin.X = frameWidth - origin.X;

        spriteBatch.Draw(_activeTex, Position, sourceRect, KnightTint, 0f, origin, _knightScale, _facing, 0f);
    }
}
