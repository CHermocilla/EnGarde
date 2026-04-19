using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Audio;

namespace RPG_DEMO_PROJ
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Dictionary<string, Texture2D> _knightAnimations;

        private Texture2D _bgSky, _bgMountains, _bgFloor, _bgWindows, _bgColumns, _bgDragon, _bgCandles;
        private SpriteFont _medievalFont;

        public enum GameState { MainMenu, Playing, Controls, Credits, GameOver }
        private GameState _currentState = GameState.MainMenu;

        private int _selectedOption = 0;
        private string[] _menuOptions = { "P1 vs CPU", "P1 vs P2", "Controls", "Credits", "Exit" };
        private KeyboardState _prevKState;

        private Player _player1;
        private Player _player2;
        private bool _isVsCPU = false;

        private Texture2D _pixel;

        private float _shakeIntensity = 0f;
        private Random _random = new Random();
        private float _hitStopTimer = 0f;

        private float _punishTextTimer = 0f;
        private string _punishText = "";

        private int _p1Rounds = 0;
        private int _p2Rounds = 0;

        private float _roundResetTimer = 0f;
        private bool _roundEnding = false;

        private float _roundIntroTimer = 0f;
        private bool _roundStarting = false;
        private string _roundIntroText = "";

        // AUDIO
        private Song _menuMusic;
        private Song _battleMusic;

        private SoundEffect _hoverSfx;
        private SoundEffect _confirmSfx;
        private SoundEffect _swingSfx;
        private SoundEffect _hitSfx;

        private SoundEffect _blockSfx;

        private string _currentMusic = "";

        private Player.PlayerState _prevP1State = Player.PlayerState.Idle;
        private Player.PlayerState _prevP2State = Player.PlayerState.Idle;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _medievalFont = Content.Load<SpriteFont>("MedievalFont");

            _knightAnimations = new Dictionary<string, Texture2D>();

            // Basic movement
            _knightAnimations.Add("idle", Content.Load<Texture2D>("knight_idle"));
            _knightAnimations.Add("walk", Content.Load<Texture2D>("knight_walk"));
            _knightAnimations.Add("run", Content.Load<Texture2D>("knight_run"));
            _knightAnimations.Add("jump", Content.Load<Texture2D>("knight_jump"));

            // Combat
            _knightAnimations.Add("attack1", Content.Load<Texture2D>("knight_attack1"));
            _knightAnimations.Add("attack2", Content.Load<Texture2D>("knight_attack2"));
            _knightAnimations.Add("attack3", Content.Load<Texture2D>("knight_attack3"));
            _knightAnimations.Add("runattack", Content.Load<Texture2D>("knight_run_attack"));
            _knightAnimations.Add("defend", Content.Load<Texture2D>("knight_defend"));
            _knightAnimations.Add("hurt", Content.Load<Texture2D>("knight_hurt"));

            // Backgrounds
            _bgSky = Content.Load<Texture2D>("bg");
            _bgMountains = Content.Load<Texture2D>("mountaims");
            _bgWindows = Content.Load<Texture2D>("wall@windows");
            _bgFloor = Content.Load<Texture2D>("floor");
            _bgColumns = Content.Load<Texture2D>("columns&falgs");
            _bgCandles = Content.Load<Texture2D>("candeliar");
            _bgDragon = Content.Load<Texture2D>("dragon");

            // AUDIO
            _menuMusic = Content.Load<Song>("Main_Menu");
            _battleMusic = Content.Load<Song>("Battle_Music");

            _hoverSfx = Content.Load<SoundEffect>("Hover");
            _confirmSfx = Content.Load<SoundEffect>("Confirm");
            _swingSfx = Content.Load<SoundEffect>("swing");
            _hitSfx = Content.Load<SoundEffect>("hit");
            _blockSfx = Content.Load<SoundEffect>("block");

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0.45f;

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        private void PlayMusic(string musicName)
        {
            if (_currentMusic == musicName)
                return;

            if (musicName == "menu")
                MediaPlayer.Play(_menuMusic);
            else if (musicName == "battle")
                MediaPlayer.Play(_battleMusic);

            _currentMusic = musicName;
        }

        private bool IsAttackState(Player.PlayerState state)
        {
            return state == Player.PlayerState.Attack1 ||
                   state == Player.PlayerState.Attack2 ||
                   state == Player.PlayerState.Attack3 ||
                   state == Player.PlayerState.RunAttack;
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState kstate = Keyboard.GetState();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_hitStopTimer > 0f)
            {
                _hitStopTimer -= dt;
                _prevKState = kstate;
                return;
            }

            if (_shakeIntensity > 0f)
                _shakeIntensity -= 0.6f;
            else
                _shakeIntensity = 0f;

            if (_punishTextTimer > 0f)
            {
                _punishTextTimer -= dt;
                if (_punishTextTimer <= 0f)
                {
                    _punishTextTimer = 0f;
                    _punishText = "";
                }
            }

            switch (_currentState)
            {
                case GameState.MainMenu:
                    PlayMusic("menu");
                    _shakeIntensity = 0f;

                    if (kstate.IsKeyDown(Keys.Down) && _prevKState.IsKeyUp(Keys.Down))
                    {
                        _selectedOption = (_selectedOption + 1) % _menuOptions.Length;
                        _hoverSfx.Play(0.55f, 0f, 0f);
                    }

                    if (kstate.IsKeyDown(Keys.Up) && _prevKState.IsKeyUp(Keys.Up))
                    {
                        _selectedOption = (_selectedOption - 1 + _menuOptions.Length) % _menuOptions.Length;
                        _hoverSfx.Play(0.55f, 0f, 0f);
                    }

                    if (kstate.IsKeyDown(Keys.Enter) && _prevKState.IsKeyUp(Keys.Enter))
                    {
                        _confirmSfx.Play(0.7f, 0f, 0f);

                        switch (_selectedOption)
                        {
                            case 0:
                                _isVsCPU = true;
                                ResetMatch();
                                _currentState = GameState.Playing;
                                break;

                            case 1:
                                _isVsCPU = false;
                                ResetMatch();
                                _currentState = GameState.Playing;
                                break;

                            case 2:
                                _currentState = GameState.Controls;
                                break;

                            case 3:
                                _currentState = GameState.Credits;
                                break;

                            case 4:
                                Exit();
                                break;
                        }
                    }
                    break;

                case GameState.Playing:
                    PlayMusic("battle");

                    if (_player1 != null && _player2 != null)
                    {
                        // Round intro pause
                        if (_roundStarting)
                        {
                            _roundIntroTimer -= dt;

                            if (_roundIntroTimer < 1.0f)
                                _roundIntroText = "FIGHT!";

                            if (_roundIntroTimer <= 0f)
                            {
                                _roundStarting = false;
                                _roundIntroTimer = 0f;
                            }

                            _prevKState = kstate;
                            break;
                        }

                        _player1.Update(gameTime, GraphicsDevice.Viewport.Width, _player2);
                        _player2.Update(gameTime, GraphicsDevice.Viewport.Width, _player1);

                        _player1.ResolvePlayerCollision(_player2);

                        // Swing SFX when attack starts
                        if (IsAttackState(_player1._currentState) && !IsAttackState(_prevP1State))
                            _swingSfx.Play(0.45f, 0f, 0f);

                        if (IsAttackState(_player2._currentState) && !IsAttackState(_prevP2State))
                            _swingSfx.Play(0.45f, 0f, 0f);

                        if (_player1.JustTriggeredPunish() || _player2.JustTriggeredPunish())
                        {
                            _punishText = "PUNISH!";
                            _punishTextTimer = 0.6f;
                        }

                        if (_player1.JustBlockedHit() || _player2.JustBlockedHit())
                        {
                            _blockSfx.Play(0.65f, 0f, 0f);
                        }
                        else if (_player1.JustDealtDamage() || _player2.JustDealtDamage())
                        {
                            _hitSfx.Play(0.65f, 0f, 0f);

                            bool finisherHit = _player1.IsUsingFinisher() || _player2.IsUsingFinisher();

                            _hitStopTimer = finisherHit ? 0.05f : 0.03f;
                            _shakeIntensity = finisherHit ? 4f : 2f;
                        }

                        if (!_roundEnding && (_player1.IsDead || _player2.IsDead))
                        {
                            _roundEnding = true;
                            _roundResetTimer = 1.5f;

                            if (_player1.IsDead)
                                _p2Rounds++;
                            else
                                _p1Rounds++;
                        }

                        if (_roundEnding)
                        {
                            _roundResetTimer -= dt;

                            if (_roundResetTimer <= 0f)
                            {
                                _roundEnding = false;
                                _roundResetTimer = 0f;

                                if (_p1Rounds >= 2 || _p2Rounds >= 2)
                                {
                                    _currentState = GameState.GameOver;
                                }
                                else
                                {
                                    var p1Controls = new Player.PlayerControls
                                    {
                                        Left = Keys.A,
                                        Right = Keys.D,
                                        Attack1 = Keys.W,
                                        Attack2 = Keys.S
                                    };

                                    var p2Controls = new Player.PlayerControls
                                    {
                                        Left = Keys.Left,
                                        Right = Keys.Right,
                                        Attack1 = Keys.Up,
                                        Attack2 = Keys.Down
                                    };

                                    _player1 = new Player(_knightAnimations, new Vector2(300, 600), p1Controls)
                                    {
                                        Label = "P1"
                                    };

                                    _player2 = new Player(_knightAnimations, new Vector2(900, 600), p2Controls)
                                    {
                                        Label = _isVsCPU ? "CPU" : "P2",
                                        KnightTint = Color.Crimson
                                    };

                                    _punishText = "";
                                    _punishTextTimer = 0f;
                                    _shakeIntensity = 0f;
                                    _hitStopTimer = 0f;

                                    _prevP1State = Player.PlayerState.Idle;
                                    _prevP2State = Player.PlayerState.Idle;

                                    _roundStarting = true;
                                    _roundIntroTimer = 2.5f;
                                    _roundIntroText = "ROUND " + (_p1Rounds + _p2Rounds + 1);
                                }
                            }
                        }


                        _prevP1State = _player1._currentState;
                        _prevP2State = _player2._currentState;
                    }

                    if (kstate.IsKeyDown(Keys.Escape) && _prevKState.IsKeyUp(Keys.Escape))
                        _currentState = GameState.MainMenu;
                    break;

                case GameState.GameOver:
                    PlayMusic("menu");

                    if (kstate.IsKeyDown(Keys.Enter) && _prevKState.IsKeyUp(Keys.Enter))
                    {
                        _confirmSfx.Play(0.7f, 0f, 0f);
                        _currentState = GameState.MainMenu;
                    }
                    break;

                case GameState.Controls:
                    PlayMusic("menu");

                    if (kstate.IsKeyDown(Keys.Escape) && _prevKState.IsKeyUp(Keys.Escape))
                    {
                        _hoverSfx.Play(0.5f, 0f, 0f);
                        _currentState = GameState.MainMenu;
                    }
                    break;

                case GameState.Credits:
                    PlayMusic("menu");

                    if (kstate.IsKeyDown(Keys.Escape) && _prevKState.IsKeyUp(Keys.Escape))
                    {
                        _hoverSfx.Play(0.5f, 0f, 0f);
                        _currentState = GameState.MainMenu;
                    }
                    break;
            }

            _prevKState = kstate;
            base.Update(gameTime);
        }

        private void ResetMatch()
        {
            _p1Rounds = 0;
            _p2Rounds = 0;
            _roundEnding = false;
            _roundResetTimer = 0f;

            var p1Controls = new Player.PlayerControls
            {
                Left = Keys.A,
                Right = Keys.D,
                Attack1 = Keys.W,
                Attack2 = Keys.S
            };

            var p2Controls = new Player.PlayerControls
            {
                Left = Keys.Left,
                Right = Keys.Right,
                Attack1 = Keys.Up,
                Attack2 = Keys.Down
            };

            _player1 = new Player(_knightAnimations, new Vector2(300, 600), p1Controls)
            {
                Label = "P1"
            };

            _player2 = new Player(_knightAnimations, new Vector2(900, 600), p2Controls)
            {
                Label = _isVsCPU ? "CPU" : "P2",
                KnightTint = Color.Crimson
            };

            _prevP1State = Player.PlayerState.Idle;
            _prevP2State = Player.PlayerState.Idle;

            _punishText = "";
            _punishTextTimer = 0f;
            _shakeIntensity = 0f;
            _hitStopTimer = 0f;

            _roundStarting = true;
            _roundIntroTimer = 2.5f;
            _roundIntroText = "ROUND 1";
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            Vector2 shakeOffset = Vector2.Zero;
            if (_shakeIntensity > 0f)
            {
                shakeOffset = new Vector2(
                    (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity,
                    (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity
                );
            }

            _spriteBatch.Begin(
                samplerState: SamplerState.PointClamp,
                transformMatrix: Matrix.CreateTranslation(shakeOffset.X, shakeOffset.Y, 0f)
            );

            int w = GraphicsDevice.Viewport.Width;
            int h = GraphicsDevice.Viewport.Height;
            Rectangle screenRect = new Rectangle(0, 0, w, h);

            if (_currentState == GameState.MainMenu)
            {
                string title = "En Garde!";
                float titleScale = 2.2f;

                Vector2 titleSize = _medievalFont.MeasureString(title) * titleScale;
                Vector2 titlePos = new Vector2((w - titleSize.X) / 2f, 120f);

                _spriteBatch.DrawString(_medievalFont, title, titlePos + new Vector2(4f, 4f), Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, title, titlePos, Color.Gold, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

                float startY = 260f;
                float spacing = 60f;

                for (int i = 0; i < _menuOptions.Length; i++)
                {
                    string option = _menuOptions[i];
                    Color textColor = (i == _selectedOption) ? Color.Gold : Color.White;

                    string displayText = (i == _selectedOption) ? $"> {option} <" : option;

                    Vector2 optionSize = _medievalFont.MeasureString(displayText);
                    Vector2 optionPos = new Vector2((w - optionSize.X) / 2f, startY + (i * spacing));

                    _spriteBatch.DrawString(_medievalFont, displayText, optionPos + new Vector2(2f, 2f), Color.Black);
                    _spriteBatch.DrawString(_medievalFont, displayText, optionPos, textColor);
                }
            }
            else if (_currentState == GameState.Playing)
            {
                _spriteBatch.Draw(_bgSky, screenRect, Color.White);
                _spriteBatch.Draw(_bgMountains, screenRect, Color.White);
                _spriteBatch.Draw(_bgWindows, screenRect, Color.White);
                _spriteBatch.Draw(_bgCandles, screenRect, Color.White);
                _spriteBatch.Draw(_bgDragon, screenRect, Color.White);
                _spriteBatch.Draw(_bgFloor, screenRect, Color.White);
                _spriteBatch.Draw(_bgColumns, screenRect, Color.White);

                if (_player1 != null && _player2 != null)
                {
                    string roundText = $"Round {_p1Rounds + _p2Rounds + 1}";
                    _spriteBatch.DrawString(_medievalFont, roundText, new Vector2(w / 2 - 60, 50), Color.White);

                    _player1.Draw(_spriteBatch);
                    _player2.Draw(_spriteBatch);

                    _spriteBatch.DrawString(_medievalFont, _player1.Label,
                        new Vector2(_player1.Position.X - 20, _player1.Position.Y - 150), Color.White);

                    _spriteBatch.DrawString(_medievalFont, _player2.Label,
                        new Vector2(_player2.Position.X - 20, _player2.Position.Y - 150), Color.Crimson);

                    for (int i = 0; i < _player1.Health; i++)
                        _spriteBatch.Draw(_pixel, new Rectangle(50 + (i * 30), 50, 20, 20), Color.Gold);

                    for (int i = 0; i < _player2.Health; i++)
                        _spriteBatch.Draw(_pixel, new Rectangle(w - 150 + (i * 30), 50, 20, 20), Color.Crimson);

                    for (int i = 0; i < _p1Rounds; i++)
                        _spriteBatch.Draw(_pixel, new Rectangle(50 + (i * 25), 110, 18, 18), Color.Gold);

                    for (int i = 0; i < _p2Rounds; i++)
                        _spriteBatch.Draw(_pixel, new Rectangle(w - 150 + (i * 25), 110, 18, 18), Color.Crimson);

                    _spriteBatch.Draw(_pixel, new Rectangle(50, 80, 200, 12), Color.DarkSlateGray);
                    _spriteBatch.Draw(_pixel,
                        new Rectangle(50, 80, (int)(200 * (_player1.BlockStamina / _player1.MaxBlockStamina)), 12),
                        Color.Cyan);

                    _spriteBatch.Draw(_pixel, new Rectangle(w - 250, 80, 200, 12), Color.DarkSlateGray);
                    _spriteBatch.Draw(_pixel,
                        new Rectangle(w - 250, 80, (int)(200 * (_player2.BlockStamina / _player2.MaxBlockStamina)), 12),
                        Color.OrangeRed);
                }

                if (_punishTextTimer > 0f && !string.IsNullOrEmpty(_punishText))
                {
                    float punishScale = 2.5f;

                    Vector2 baseSize = _medievalFont.MeasureString(_punishText);
                    Vector2 scaledSize = baseSize * punishScale;
                    Vector2 textPos = new Vector2((w - scaledSize.X) / 2f, 120f);

                    _spriteBatch.DrawString(_medievalFont, _punishText, textPos + new Vector2(3f, 3f), Color.DarkRed, 0f, Vector2.Zero, punishScale, SpriteEffects.None, 0f);
                    _spriteBatch.DrawString(_medievalFont, _punishText, textPos, Color.Black, 0f, Vector2.Zero, punishScale, SpriteEffects.None, 0f);
                }

                if (_roundEnding)
                {
                    string endText = "ROUND OVER";
                    Vector2 size = _medievalFont.MeasureString(endText) * 1.6f;
                    Vector2 pos = new Vector2((w - size.X) / 2f, 200);

                    _spriteBatch.DrawString(_medievalFont, endText, pos + new Vector2(3f, 3f), Color.Black, 0f, Vector2.Zero, 1.6f, SpriteEffects.None, 0f);
                    _spriteBatch.DrawString(_medievalFont, endText, pos, Color.Gold, 0f, Vector2.Zero, 1.6f, SpriteEffects.None, 0f);
                }

                if (_roundStarting)
                {
                    float introScale = (_roundIntroText == "FIGHT!") ? 3.0f : 2.5f;

                    Vector2 size = _medievalFont.MeasureString(_roundIntroText) * introScale;
                    Vector2 pos = new Vector2((w - size.X) / 2f, 200);

                    _spriteBatch.DrawString(_medievalFont, _roundIntroText, pos + new Vector2(4f, 4f), Color.Black, 0f, Vector2.Zero, introScale, SpriteEffects.None, 0f);
                    _spriteBatch.DrawString(_medievalFont, _roundIntroText, pos, Color.Gold, 0f, Vector2.Zero, introScale, SpriteEffects.None, 0f);
                }
            }
            else if (_currentState == GameState.Controls)
            {
                string title = "CONTROLS";

                string leftText =
                    "PLAYER 1\n" +
                    "A / D  - Move\n" +
                    "W      - Quick Attack\n" +
                    "S      - Heavy / Finisher\n\n" +
                    "PLAYER 2\n" +
                    "LEFT / RIGHT - Move\n" +
                    "UP           - Quick Attack\n" +
                    "DOWN         - Heavy / Finisher\n\n" +
                    "DEFENSE\n" +
                    "Hold BACK to Block\n" +
                    "Double-tap BACK to Backdash\n" +
                    "Blocking drains Guard Stamina\n" +
                    "Guard Break leaves you vulnerable";

                string rightText =
                    "COMBAT\n" +
                    "Quick Attack = fast poke\n" +
                    "Heavy Attack = punish starter\n" +
                    "Heavy punish opens Finisher window\n" +
                    "Finisher deals heavy damage\n\n" +
                    "MATCH RULES\n" +
                    "Best of 3 rounds\n" +
                    "First to 2 wins the match\n\n" +
                    "MENUS\n" +
                    "UP / DOWN - Select\n" +
                    "ENTER     - Confirm\n" +
                    "ESC       - Return";

                float titleScale = 1.6f;
                float textScale = 0.80f;

                Vector2 titleSize = _medievalFont.MeasureString(title) * titleScale;
                Vector2 titlePos = new Vector2((w - titleSize.X) / 2f, 55f);

                _spriteBatch.DrawString(_medievalFont, title, titlePos + new Vector2(3f, 3f), Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, title, titlePos, Color.Gold, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

                Vector2 leftSize = _medievalFont.MeasureString(leftText) * textScale;
                Vector2 rightSize = _medievalFont.MeasureString(rightText) * textScale;

                float columnGap = 80f;
                float totalWidth = leftSize.X + columnGap + rightSize.X;

                float startX = (w - totalWidth) / 2f;
                float startY = 140f;

                Vector2 leftPos = new Vector2(startX, startY);
                Vector2 rightPos = new Vector2(startX + leftSize.X + columnGap, startY);

                _spriteBatch.DrawString(_medievalFont, leftText, leftPos + new Vector2(2f, 2f), Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, leftText, leftPos, Color.White, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

                _spriteBatch.DrawString(_medievalFont, rightText, rightPos + new Vector2(2f, 2f), Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, rightText, rightPos, Color.White, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }
            else if (_currentState == GameState.Credits)
            {
                string title = "CREDITS";

                string leftText =
                    "Programmer:\n" +
                    "Ceeline Hermocilla\n\n" +

                    "Group Members:\n" +
                    "Amerah Saripada\n" +
                    "Angelika Gere\n" +
                    "Armin Emmanuel Patulan\n" +
                    "Joshua Olmedo\n" +
                    "Pauline Anne Rivera";

                string rightText =
                    "Free Game Assets:\n" +
                    "sfx.productioncrate.com\n" +
                    "fulminisictus (itch.io)\n" +
                    "boris-sandor (itch.io)\n" +
                    "craftpix.net\n\n" +

                    "Built With:\n" +
                    "MonoGame Framework\n" +
                    "C# / Visual Studio";

                string footerText =
                    "Game Dev Project\n" +
                    "BSIT 3 - 1 / Prof. Renjun Orain\n\n" +
                    "March 2026";

                float titleScale = 1.4f;
                float textScale = 0.75f;
                float footerScale = 0.9f;
                float escScale = 0.8f;

                // TITLE
                Vector2 titleSize = _medievalFont.MeasureString(title) * titleScale;
                Vector2 titlePos = new Vector2((w - titleSize.X) / 2f, 70f);

                _spriteBatch.DrawString(_medievalFont, title, titlePos + new Vector2(3, 3), Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, title, titlePos, Color.Gold, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

                // COLUMN SIZES
                Vector2 leftSize = _medievalFont.MeasureString(leftText) * textScale;
                Vector2 rightSize = _medievalFont.MeasureString(rightText) * textScale;

                float columnGap = 120f;
                float columnWidth = Math.Max(leftSize.X, rightSize.X);
                float totalWidth = columnWidth * 2 + columnGap;

                float startX = (w - totalWidth) / 2f;
                float startY = 160f;

                Vector2 leftPos = new Vector2(startX, startY);
                Vector2 rightPos = new Vector2(startX + columnWidth + columnGap, startY);

                // LEFT COLUMN
                _spriteBatch.DrawString(_medievalFont, leftText, leftPos + new Vector2(2, 2), Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, leftText, leftPos, Color.White, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

                // RIGHT COLUMN
                _spriteBatch.DrawString(_medievalFont, rightText, rightPos + new Vector2(2, 2), Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, rightText, rightPos, Color.White, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

                // FOOTER
                Vector2 footerSize = _medievalFont.MeasureString(footerText) * footerScale;

                Vector2 footerPos = new Vector2(
                    (w - footerSize.X) / 2f,
                    startY + Math.Max(leftSize.Y, rightSize.Y) + 40f
                );

                _spriteBatch.DrawString(_medievalFont, footerText, footerPos + new Vector2(2, 2), Color.Black, 0f, Vector2.Zero, footerScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, footerText, footerPos, Color.White, 0f, Vector2.Zero, footerScale, SpriteEffects.None, 0f);

                // ESC TEXT aligned to footer center
                string escText = "Press ESC to return";

                Vector2 escSize = _medievalFont.MeasureString(escText) * escScale;

                float footerCenterX = footerPos.X + (footerSize.X / 2f);

                Vector2 escPos = new Vector2(
                    footerCenterX - (escSize.X / 2f) + 4f,
                    footerPos.Y + footerSize.Y + 25f
                );

                _spriteBatch.DrawString(_medievalFont, escText, escPos + new Vector2(2, 2), Color.Black, 0f, Vector2.Zero, escScale, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, escText, escPos, Color.White * 0.7f, 0f, Vector2.Zero, escScale, SpriteEffects.None, 0f);
            }
            else if (_currentState == GameState.GameOver)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), Color.Black * 0.6f);

                string winnerName = _p1Rounds >= 2 ? "PLAYER 1" : "PLAYER 2";
                Color winnerColor = _p1Rounds >= 2 ? Color.Gold : Color.Crimson;

                string winText = winnerName + " WINS THE MATCH";
                string subText = "Press ENTER to return to Main Menu";

                Vector2 winSize = _medievalFont.MeasureString(winText) * 1.5f;
                Vector2 winPos = new Vector2((w - winSize.X) / 2f, h / 2f - 70f);

                Vector2 subSize = _medievalFont.MeasureString(subText);
                Vector2 subPos = new Vector2((w - subSize.X) / 2f, h / 2f + 20f);

                _spriteBatch.DrawString(_medievalFont, winText, winPos + new Vector2(3f, 3f), Color.Black, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_medievalFont, winText, winPos, winnerColor, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);

                _spriteBatch.DrawString(_medievalFont, subText, subPos + new Vector2(2f, 2f), Color.Black);
                _spriteBatch.DrawString(_medievalFont, subText, subPos, Color.White * 0.85f);
            }

            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}

