using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;

namespace pexeso_client;

public enum GameState
{
    CONNECTING,
    INGAME,
    WINSCREEN
}

public class MultiplayerManager
{
    private Game1 _instance;
    public WebSocket connection;
    public string _username;
    public int id;
    public MultiplayerManager(string url, string username)
    {
        _username = username;
        connection = new WebSocket("ws://"+ url + "/player");
        connection.OnMessage += OnMessage;
        connection.OnOpen += OnOpen;
        connection.OnClose += OnClose;
        _instance = new pexeso_client.Game1(this);
        connection.Connect();
        _instance.Run();
    }

    private void OnClose(object sender, CloseEventArgs e)
    {
        Console.WriteLine("Connection Lost.");
        _instance.Exit();
    }

    private void OnOpen(object sender, EventArgs e)
    {
        connection.Send("username:" + _username);
    }

    private void OnMessage(object sender, MessageEventArgs e)
    {
        Console.WriteLine(e.Data);
        if (e.Data.StartsWith("card"))
        {
            string dataString = e.Data.Split("card:")[1];
            List<string> data = dataString.Split(',').ToList();
            _instance.grid[Convert.ToInt32(data[0]), Convert.ToInt32(data[1])] = Convert.ToInt32(data[2]);
        }
        else if (e.Data.StartsWith("clearRevealedCards"))
        {
            _instance.revealedCards.Clear();
        }
        else if (e.Data.StartsWith("revealedCard"))
        {
            string dataString = e.Data.Split("revealedCard:")[1];
            List<string> data = dataString.Split(',').ToList();
            _instance.revealedCards.Add(new Vector2(Convert.ToInt32(data[0]), Convert.ToInt32(data[1])));
        }
        else if (e.Data.StartsWith("GameStarted"))
        {
            _instance.gameState = GameState.INGAME;
        }
        else if (e.Data.StartsWith("gridsize:"))
        {
            string dataString = e.Data.Split("gridsize:")[1];
            int width = Convert.ToInt32(dataString.Split(',')[0]);
            int height = Convert.ToInt32(dataString.Split(',')[1]);
            _instance.gridWidth = width;
            _instance.gridHeight = height;
        }
        else if (e.Data.StartsWith("GameEnded"))
        {
            _instance.gameState = GameState.WINSCREEN;
            int playerWon = Convert.ToInt32(e.Data.Split(":")[1]);
            _instance.playerWon = playerWon;
        }
        else if (e.Data.StartsWith("id:"))
        {
            int id = Convert.ToInt32(e.Data.Split("id:")[1]);
            this.id = id;
        }
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
    public int gridWidth = 4;
    public int gridHeight = 3;
    int cardsOnGrid;
    public int[,] grid;

    int turn = 0;

    private List<int> scores = new List<int>() { 0, 0 };

    public List<Vector2> revealedCards = new List<Vector2>();

    private MultiplayerManager _manager;

    public GameState gameState = GameState.CONNECTING;
    public int playerWon = 0;
    public Game1(MultiplayerManager manager)
    {
        _manager = manager;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    public void resetWindowSize()
    {
        _graphics.PreferredBackBufferWidth = gridWidth * cardSize + ((gridWidth-1)*margin);
        _graphics.PreferredBackBufferHeight = gridHeight * cardSize + ((gridHeight-1)*margin);
        _graphics.ApplyChanges();
    }
    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        cardSprites = new List<Texture2D>(cardAmount);
        resetWindowSize();
        cardsOnGrid = gridWidth * gridHeight;
        grid = new int[gridWidth,gridHeight];
        int cardType = 1;
        for(int i = 0; i < cardsOnGrid / 2; i++){
            int x = _rand.Next(0, gridWidth);
            int y = _rand.Next(0, gridHeight);
            while(grid[x,y] != 0){
                x = _rand.Next(0, gridWidth);
                y = _rand.Next(0, gridHeight);
            }
            grid[x,y] = cardType;
            x = _rand.Next(0, gridWidth);
            y = _rand.Next(0, gridHeight);
            while(grid[x,y] != 0){
                x = _rand.Next(0, gridWidth);
                y = _rand.Next(0, gridHeight);
            }
            grid[x,y] = cardType;
            cardType++;
            if(cardType>cardAmount) cardType = 1;
        }
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        cardBackSprite = Content.Load<Texture2D>("./sprites/card-back");
        for(int i = 1; i <= cardAmount; i++){
            cardSprites.Add(Content.Load<Texture2D>($"./sprites/{i}"));
        }

        lobbyFont = Content.Load<SpriteFont>("Lobby");
        // TODO: use this.Content to load your game content here
    }

    ButtonState previousMouseState = ButtonState.Released;
    ButtonState currentMouseState = ButtonState.Released;

    protected override void Update(GameTime gameTime)
    {
        previousMouseState = currentMouseState;
        currentMouseState = Mouse.GetState().LeftButton;
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        if(currentMouseState == ButtonState.Pressed && previousMouseState == ButtonState.Released && IsActive){
            Vector2 mousePosition = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
            _manager.connection.Send($"reveal:{mousePosition.X},{mousePosition.Y}");
        }
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
        else if (gameState == GameState.WINSCREEN)
        {
            string text = "You Lost.";
            if(playerWon == _manager.id) text = "You Won!";
            _spriteBatch.DrawString(lobbyFont, text, new Vector2(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2), Color.Black);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
