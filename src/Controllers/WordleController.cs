using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace src.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WordleController : ControllerBase
    {

        #region Consts

        private const int MaxGuesses = 6;
        private const int WordLength = 5;
        #endregion

        WordleRepository _WordleRepository;
        private readonly IMemoryCache _memoryCache;

        public WordleController(WordleRepository wordleRepository, IMemoryCache memoryCache)
        {
            _WordleRepository = wordleRepository;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Example of calls
        /// </summary>
        /// <returns>string</returns>
        [HttpGet("Test")]
        public async Task<ActionResult> Test()
        {
            _WordleRepository.CreateWordle();

            var isOkWord = _WordleRepository.IsAWord("Aalst");

            var response = _WordleRepository.CheckWord("abcde");

            return Ok(response);
        }

        //What to add 
        //the function should get customer identifier (could be anything)
        //this will be used in allow only 6 guess
        //Cache example in WordleRepository


        //function (not get) to start game


        //function (not get) to check word exists in dictionary

        //function (not get) to check word against current word
        //please make sure that you return a better object then "01020"
        //Add support for swagger


        //function here to allow only 6 guess - using cache by customer
        //so you need to store the number of guess


        //all function should have validation + correct http status to return
        // 200, 403 if there is an error


        /// <summary>
        /// Starts a new game for a user, if the user already in an open game, the game will be reset (for the sake of simplicity)
        /// </summary>
        /// <param name="request">Holds user name to start a game</param>
        /// <returns></returns>
        [HttpPost("StartGame")]
        public async Task<ActionResult> StartGame(StartGameRequest request)
        {
            _WordleRepository.CreateWordle();

            var gameModel = new GameModel(request.UserName);

            _memoryCache.Set<GameModel>(request.UserName, gameModel);


            return Ok(new StartGameResponse() { Game = gameModel });
        }

        /// <summary>
        /// checks if a word exists in the dictionary stored on the server and is 5 characters long
        ///</summary>
        ///<param name="request">Holds a word to check</param>
        [HttpPost("IsAWord")]
        public async Task<ActionResult> IsAWord(ISAWordWordRequest request)
        {
            var result = _WordleRepository.IsAWord(request.Word);

            return Ok(new ISAWordWordResponse() { Result = result });
        }

        /// <summary>
        /// The main task in the game: check for the requested user and word, which letters match and how, 
        /// advances the game details (state and guesses) and returns the new data.
        ///</summary>
        ///<param name="request">Holds user name and word to check and advance the game by</param>
        [HttpPost("CheckWord")]
        public async Task<ActionResult> CheckWord(CheckWordRequest request)
        {

            var game =  _memoryCache.Get<GameModel>(request.UserName);
            if (game == null) { 
                return StatusCode(StatusCodes.Status403Forbidden, "User not in a game");
            }

            var requestWord = request.Word.ToLower();

            //validations
            if (game.GameStatus != GameStatuses.InProgress)
            {
                return StatusCode(StatusCodes.Status403Forbidden, $"Game is not in progress, user has already {(game.GameStatus==GameStatuses.Won ? "won" : "lost")} the game");
            }
            if (requestWord.Length != WordLength)
            {
                return StatusCode(StatusCodes.Status403Forbidden, $"Word must be {WordLength} characters long");
            }
            //a nice-to-have validation
            if (!_WordleRepository.IsAWord(request.Word))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Requested word does not exist in the dictionary");
            }
            //check requested word against correct word, build letter checks object, and calculate new game object
            var checkWordResultString = _WordleRepository.CheckWord(request.Word);
            var letterChecks = LetterChecksFromString(request.Word, checkWordResultString);

            GameStatuses newGameStatus = IsMatchSuccess(checkWordResultString)? GameStatuses.Won : 
                game.Guesses >= MaxGuesses-1 ? GameStatuses.Lost :
                GameStatuses.InProgress;

            var newGame = new GameModel(game.UserName, game.Guesses + 1, newGameStatus);

            //save the game in the cache
            _memoryCache.Set<GameModel>(request.UserName, newGame);

            return Ok(new CheckWordResponse() { LetterChecks = letterChecks, Game = newGame });
        }


        #region Private Methods

        #region CheckWord methods

        // converts a string of 0,1,2's respresenting match types of letters for a requested word, to an array of LetterCheck objects
        // basically converts every match character to a LetterCheck object
        private IEnumerable<LetterCheck> LetterChecksFromString(string requestWord, string checkWordString)
        {
            return requestWord.Select((ch, index) => new LetterCheck() { Letter = ch, Position = index, MatchStrength = ((LetterMatchStrength)int.Parse(checkWordString[index].ToString())).ToString() }).ToArray();
        }

        private bool IsMatchSuccess(string checkWordResultString)
        {
            return checkWordResultString.All(ch => ch == '2');
        }

        #endregion

        #endregion


    }

    #region Models (should be in different files)

    #region requests and responses

    #region StartGame
    public class StartGameRequest
    {
        public string UserName { get; set; }
    }
    public class StartGameResponse
    {
        public GameModel Game { get; set; }
    }
    #endregion

    #region IsAWord
    public class ISAWordWordRequest
    {
        public string Word { get; set; }
    }
    public class ISAWordWordResponse
    {
        public bool Result { get; set; }
    }
    #endregion

    #region CheckWord
    public class CheckWordRequest
    {
        public string UserName { get; set; }
        public string Word { get; set; }
    }
    public class CheckWordResponse
    {
        public IEnumerable<LetterCheck> LetterChecks{ get; set; }
        public GameModel Game { get; set; }

    }
    #endregion

    #endregion


    #region GameModel and related
    public class LetterCheck
    {
        public char Letter { get; set; }
        public int Position { get; set; }
        public string MatchStrength { get; set; }
    }
    public enum LetterMatchStrength
    {
        NotInWord=0,
        InWordWrongPlace=1,
        InWordRightPlace=2
    }

    public class GameModel
    {
        public string UserName { get; set; }
        public int Guesses { get; set; }
        public GameStatuses GameStatus { get; set; }
        public string GameStatusDesc { get; set; }

        public GameModel(string userName)
        {
            UserName = userName;
            Guesses = 0;
            GameStatus = GameStatuses.InProgress;
            GameStatusDesc = GameStatus.ToString();
        }
        public GameModel(string userName, int guesses, GameStatuses gamestatus)
        {
            UserName = userName;
            Guesses = guesses;
            GameStatus = gamestatus;
            GameStatusDesc = GameStatus.ToString();
        }
    }
    public enum GameStatuses {
           InProgress,
           Won,
           Lost
       }
    #endregion

    #endregion
}
