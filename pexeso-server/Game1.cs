using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net;

namespace pexeso_server;

public enum GameState
{
    LOBBY,
    INGAME,
    WINSCREEN
}

public class Match
{
    private Game1 _instance;
    public List<PlayerManager> players = new List<PlayerManager>(2);
    public WebSocketServer server { get; set; }

    public Match(string port)
    {
        server = new WebSocketServer($"ws://0.0.0.0:{port}");
        server.AddWebSocketService<PlayerManager>("/player", () => new PlayerManager(_instance, this));
        server.Start();
        _instance = new pexeso_server.Game1(this);
        _instance.Run();
    }

    public void SendToAll(string message)
    {
        foreach (PlayerManager player in players)
        {
            player.Context.WebSocket.Send(message);
        }
    }
}

public class PlayerManager : WebSocketBehavior
{
    private Game1 _instance;
    private Match _match;
    private int id;
    public string _username;
    public PlayerManager(Game1 instance, Match match)
    {
        _match = match;
        
        _instance = instance;
        if(_instance.playersConnected >= 2) Context.WebSocket.Close();
    }
    protected override void OnOpen()
    {
        id = _match.players.Count;
        _match.players.Add(this);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        Console.WriteLine(e.Data);
        if (e.Data.StartsWith("reveal"))
        {
            string positionString = e.Data.Split("reveal:")[1];
            Vector2 position = new Vector2(Convert.ToInt32(positionString.Split(',')[0]), Convert.ToInt32(positionString.Split(',')[1]));
            _instance.RevealCard(position, id);
        }
        else if (e.Data.StartsWith("username:"))
        {
            _username = e.Data.Split("username:")[1];
            Console.WriteLine($"Player {_username} Connected.");
            Context.WebSocket.Send($"gridsize:{_instance.gridWidth},{_instance.gridHeight}");
            Context.WebSocket.Send($"id:{id}");
        }
    }
    
    protected override void OnClose(CloseEventArgs e)
    {
        Console.WriteLine("Lost Connection.");
        if(_instance.gameState != GameState.WINSCREEN)
            _instance.gameState = GameState.LOBBY;
        _match.players.Remove(this);
    }
}

public class Game1 : Game
{
    Random _rand = new Random();
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    Texture2D cardBackSprite;
    SpriteFont lobbyFont;
    int cardAmount = 11;
    int cardSize = 128;
    int margin = 8;
    List<Texture2D> cardSprites;
    public int gridWidth = 7;
    public int gridHeight = 4;
    int cardsOnGrid;
    int[,] grid;
    
    private List<int> scores = new List<int>() { 0, 0 };

    List<Vector2> revealedCards = new List<Vector2>();

    public GameState gameState = GameState.LOBBY;

    private Match _match;

    private int turn = 0;
    public int playersConnected = 0;

    int winningPlayer = 0;
    public Game1(Match match)
    {
        _match = match;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    public void InitGrid()
    {
        cardsOnGrid = gridWidth * gridHeight;
        grid = new int[gridWidth, gridHeight];
        int cardType = 1;
        for (int i = 0; i < cardsOnGrid / 2; i++)
        {
            int x = _rand.Next(0, gridWidth);
            int y = _rand.Next(0, gridHeight);
            while (grid[x, y] != 0)
            {
                x = _rand.Next(0, gridWidth);
                y = _rand.Next(0, gridHeight);
            }

            grid[x, y] = cardType;
            x = _rand.Next(0, gridWidth);
            y = _rand.Next(0, gridHeight);
            while (grid[x, y] != 0)
            {
                x = _rand.Next(0, gridWidth);
                y = _rand.Next(0, gridHeight);
            }

            grid[x, y] = cardType;
            cardType++;
            if (cardType > cardAmount) cardType = 1;
        }
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        cardSprites = new List<Texture2D>(cardAmount);
        _graphics.PreferredBackBufferWidth = gridWidth * cardSize + ((gridWidth - 1) * margin);
        _graphics.PreferredBackBufferHeight = gridHeight * cardSize + ((gridHeight - 1) * margin);
        _graphics.ApplyChanges();
        

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        cardBackSprite = Content.Load<Texture2D>("./sprites/card-back");
        for (int i = 1; i <= cardAmount; i++)
        {
            cardSprites.Add(Content.Load<Texture2D>($"./sprites/{i}"));
        }
        lobbyFont = Content.Load<SpriteFont>("Lobby");
        // TODO: use this.Content to load your game content here
    }

    public void RevealCard(Vector2 mousePosition, int player)
    {
        if (gameState == GameState.LOBBY) return;
        if (turn != player) return;
        mousePosition = mousePosition / (cardSize + margin);
        mousePosition = new Vector2((int)mousePosition.X, (int)mousePosition.Y);
        if (!revealedCards.Contains(mousePosition))
        {
            revealedCards.Add(mousePosition);
            if (revealedCards.Count == 2)
            {
                if (grid[(int)revealedCards[0].X, (int)revealedCards[0].Y] ==
                    grid[(int)revealedCards[1].X, (int)revealedCards[1].Y])
                {
                    grid[(int)revealedCards[0].X, (int)revealedCards[0].Y] = 0;
                    grid[(int)revealedCards[1].X, (int)revealedCards[1].Y] = 0;
                    revealedCards.Clear();
                    _match.SendToAll("clearRevealedCards");
                    scores[player]++;
                    turn++;
                    if(turn >= _match.players.Count) turn = 0;
                }
                else
                {
                    turn++;
                    if(turn >= _match.players.Count) turn = 0;
                }
                
            }
            else if (revealedCards.Count > 2)
            {
                revealedCards.Clear();
                _match.SendToAll("clearRevealedCards");
                if(turn >= _match.players.Count) turn = 0;
            }

            int emptySpaces = 0;
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (grid[x, y] == 0) emptySpaces++;
                }
            }

            if (emptySpaces == cardsOnGrid)
            {
                if (scores[0] > scores[1])
                {
                    Console.WriteLine("Player 1 wins!");
                    winningPlayer = 1;
                }
                else if (scores[1] > scores[0])
                {
                    Console.WriteLine("Player 2 wins!");
                    winningPlayer = 2;
                }
                else Console.WriteLine("Draw.");
                Console.WriteLine($"Player 1: {scores[0]}, Player 2: {scores[1]}");
                //Console.WriteLine(args[0]);
                //Exit();
                gameState = GameState.WINSCREEN;
                _match.SendToAll($"GameEnded:{winningPlayer}");
            }
            
            
        }
        UpdateGrid();
    }

    public void UpdateGrid()
    {
        if (gameState == GameState.LOBBY) return;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                _match.SendToAll($"card:{x},{y},{grid[x,y]}");
            }
        }
        foreach (Vector2 revealedCard in revealedCards)
        {
            
            _match.SendToAll($"revealedCard:{revealedCard.X},{revealedCard.Y}");
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        if (gameState == GameState.INGAME)
        {
        }
        else if (gameState == GameState.LOBBY)
        {
            if (playersConnected == 2)
            {
                if (Keyboard.GetState().IsKeyDown(Keys.Enter))
                {
                    InitGrid();
                    gameState = GameState.INGAME;
                    _match.SendToAll("GameStarted");
                }
            }
        }

        playersConnected = _match.players.Count;
        // TODO: Add your update logic here

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Gray);

        _spriteBatch.Begin();
        if (gameState == GameState.INGAME)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    //_spriteBatch.Draw(cardSprites[grid[x,y]-1], new Vector2(x*cardSize+(margin*x), y*cardSize+(margin*y)), Color.White);
                    bool revealed = false;
                    if (grid[x, y] == 0) continue;
                    foreach (Vector2 item in revealedCards)
                    {
                        if ((int)item.X == x && (int)item.Y == y)
                        {
                            revealed = true;
                            _spriteBatch.Draw(cardSprites[grid[x, y] - 1],
                                new Vector2(x * cardSize + (margin * x), y * cardSize + (margin * y)), Color.White);
                        }
                    }

                    if (revealed == false)
                    {
                        _spriteBatch.Draw(cardBackSprite,
                            new Vector2(x * cardSize + (margin * x), y * cardSize + (margin * y)), Color.White);
                    }
                }
            }
        }
        else if (gameState == GameState.LOBBY)
        {
            int i = 0;
            foreach (PlayerManager player in _match.players)
            {
                if(player._username != null)
                    _spriteBatch.DrawString(lobbyFont, player._username, Vector2.One + (Vector2.UnitY*(i*20)), Color.Black);
                i++;
            }

            if (playersConnected >= 2)
            {
                _spriteBatch.DrawString(lobbyFont, "Press Enter to start.", new Vector2(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2), Color.Black);
            }
        }
        else if (gameState == GameState.WINSCREEN)
        {
            if (winningPlayer == 0)
            {
                _spriteBatch.DrawString(lobbyFont, $"Draw.", new Vector2(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2), Color.Black);
            }
            else
            {
                _spriteBatch.DrawString(lobbyFont, $"{_match.players[winningPlayer - 1]._username} won!",
                    new Vector2(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2),
                    Color.Black);
            }
        }
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}