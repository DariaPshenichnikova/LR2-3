using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace server
{
    class Program
    {
        static int port = 8001;
        static string address = "127.0.0.1";

        //состояние одного пакета (камень, ножницы, бцмага, победа, поражение, ничья, ошибка)
        public enum enumState { stone, screes, paper, win, loos, draw, error };

        //функция возвращает название состояния по его коду
        public static string nameState(enumState state)
        {
            switch (state)
            {
                case enumState.stone:
                    return "камень";
                case enumState.screes:
                    return "ножницы";
                case enumState.paper:
                    return "бумага";
                case enumState.win:
                    return "победа";
                case enumState.loos:
                    return "поражение";
                case enumState.error:
                    return "ошибка";
                case enumState.draw:
                    return "ничья";
                default:
                    return "ошибка";
            }
        }

        //функция для написания красивого лога
        static void log(string message)
        {
            Console.WriteLine(DateTime.Now.ToString() + ": " + message);
        }

        //класс передаваемого состояния
        public class Packet
        {
            //схема валидации. проперти - поля, type - тип объекта, enum - допустимые значения поля, required - обяхательные поля
            public static JSchema schema = JSchema.Parse(
                @"{" +
                    "'type': 'object', " +
                    "'properties': " +
                    "{" +
                        "'State':  " +
                        "{" +
                            "'type':'integer', " +
                            "'enum': [0,1,2,3,4,5,6]" +
                        "}" +
                    "}, " +
                    "'required': ['State']" +
                "}");

            // поле, хранящее состояние
            public enumState State { get; set; }
            
            // конструктор
            public Packet(enumState state)
            {
                State = state;
            }

            //десериализация из json'a
            public static Packet fromJson(string json)
            {
                // объект читающий из json
                JsonTextReader reader = new JsonTextReader(new StringReader(json));
                // объект, оборачивающий читающий из json, Дополнительно валидирует с помощью схемы
                JSchemaValidatingReader validatingReader = new JSchemaValidatingReader(reader);
                validatingReader.Schema = schema;
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<Packet>(validatingReader);
            }

            //сериализация в json
            public string toJson()
            {
                StringWriter sw = new StringWriter();
                JsonTextWriter writer = new JsonTextWriter(sw);
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, this);
                return sw.ToString();
            }
        }

        
        // класс, хранящий состояние игры
        public class Game
        {
            //схема валидации
            public static JSchema schema = JSchema.Parse(
                @"{" +
                    "'type': 'object', " +
                    "'properties': " +
                    "{" +
                        "'cur': " +
                        "{" +
                            "'type':'integer', " +
                            "'enum':[0,1,2,3,4,5]" +
                        "}," +
                        "'firstRequest': " +
                        "{" +
                            "'type':'object', " +
                            "properties: " +
                            "{" +
                                "'State': " +
                                "{" +
                                    "'type':'integer', " +
                                    "'enum':[0,1,2,3,4,5,6]" +
                                "}" +
                            "}," +
                            "'required': ['State']" +
                        "}," +
                        "'secondRequest': " +
                        "{" +
                            "'type':'object', " +
                            "properties: " +
                            "{" +
                                "'State': " +
                                "{" +
                                    "'type':'integer', " +
                                    "'enum':[0,1,2,3,4,5,6]" +
                                "}" +
                            "}," +
                            "'required': ['State']" +
                        "}," +
                        "'firstResponse': " +
                        "{" +
                            "'type':'object', " +
                            "properties: " +
                            "{" +
                                "'State': " +
                                "{" +
                                    "'type':'integer', " +
                                    "'enum':[0,1,2,3,4,5,6]" +
                                "}" +
                            "}," +
                            "'required': ['State']" +
                        "}," +
                        "'secondResponse': " +
                        "{" +
                            "'type':'object', " +
                            "properties: " +
                            "{" +
                                "'State': " +
                                "{" +
                                    "'type':'integer', " +
                                    "'enum':[0,1,2,3,4,5,6]" +
                                "}" +
                            "}," +
                            "'required': ['State']" +
                        "}" +
                    "}," +
                    "'required': ['cur']" +
                "}");

            // перечисление возможных состояний. начало, первыйы прислал ход, второй прислал ход, раунд состоялся, отправлено первому, отправлено второму
            public enum lastStep { init, recFirst, recSecond, wasGame, sendFirst, sendSecond };

            // поля
            // пакет который пришлет первый (ход сделал)
            public Packet firstRequest { get; set; }
            // пакет который нужно отправить первому (результат игры)
            public Packet firstResponse { get; set; }
            // пакет который пришлет второй (ход сделал)
            public Packet secondRequest { get; set; }
            // пакет который нужно отправить второму (результат игры)
            public Packet secondResponse { get; set; }
            // состояние игры
            public lastStep cur { get; set; }

            //сокеты
            private Socket first;
            private Socket second;
            //файл для  сохранения/записи
            private string path;

            //конструктор
            public Game(Socket first, Socket second, string path)
            {
                this.first = first;
                this.second = second;
                this.path = path;
                this.cur = lastStep.init;
            }

            //инициализация по готовому объекту
            public void init(Game a)
            {
                cur = a.cur;
                firstRequest = a.firstRequest;
                firstResponse = a.firstResponse;
                secondRequest = a.secondRequest;
                secondResponse = a.secondResponse;
            }
            // разыгрывается один раунд (сравниваются присланые ходы игроков, формируются ответы игрокам)
            public void OneRound()
            {
                if (firstRequest.State == secondRequest.State)
                {
                    firstResponse = secondResponse = new Packet(enumState.draw);
                }
                else if (firstRequest.State == enumState.stone && secondRequest.State == enumState.screes ||
                    firstRequest.State == enumState.screes && secondRequest.State == enumState.paper ||
                    firstRequest.State == enumState.paper && secondRequest.State == enumState.stone)
                {
                    firstResponse = new Packet(enumState.win);
                    secondResponse = new Packet(enumState.loos);
                }
                else if (secondRequest.State == enumState.stone && firstRequest.State == enumState.screes ||
                    secondRequest.State == enumState.screes && firstRequest.State == enumState.paper ||
                    secondRequest.State == enumState.paper && firstRequest.State == enumState.stone)
                {
                    secondResponse = new Packet(enumState.win);
                    firstResponse = new Packet(enumState.loos);
                }
                else
                {
                    firstResponse = secondResponse = new Packet(enumState.error);
                }
            }

            //основной поток игры со всеми пересылками
            public void Process()
            {
                int bytes = 0;                  // количество полученных байтов
                byte[] data = new byte[256];    // буфер для получаемых данных
                while (true)
                {
                    // получаем сообщение от первого
                    StringBuilder builder = new StringBuilder();
                    //считываем в data то, что пришло
                    bytes = first.Receive(data);
                    //переводим байты в строку
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    //парсим из json'a
                    firstRequest = Packet.fromJson(builder.ToString());
                    //отмечаем, что первый прислал результат
                    cur = lastStep.recFirst;
                    //сохраняем состояние в файл
                    exportToFile(path);
                    //очищаем построитель строк
                    builder.Clear();

                    // получаем сообщение от второго - так же как и с первым
                    bytes = second.Receive(data);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    secondRequest = Packet.fromJson(builder.ToString());
                    cur = lastStep.recSecond;
                    exportToFile(path);
                    builder.Clear();

                    // производим игру
                    OneRound();
                    cur = lastStep.wasGame;
                    exportToFile(path);

                    // отправляем ответ первому
                    first.Send(Encoding.Unicode.GetBytes(firstResponse.toJson()));
                    cur = lastStep.sendFirst;
                    exportToFile(path);

                    // отправляем ответ второму
                    second.Send(Encoding.Unicode.GetBytes(secondResponse.toJson()));
                    cur = lastStep.sendSecond;

                    log("Первый: " + nameState(firstRequest.State) + "; " +
                        "Второй: " + nameState(secondRequest.State) + "; " +
                        "Итог: " + nameState(firstResponse.State) + "-" + nameState(secondResponse.State));
                    cur = lastStep.init;
                    exportToFile(path);
                }
            }

            //игра после импорта - то же самое, но некоторые шаги могут быть не выполнены если они уже выгрузились из файла
            public void GameAfterImport()
            {
                int bytes = 0;                  // количество полученных байтов
                byte[] data = new byte[256];    // буфер для получаемых данных
                StringBuilder builder = new StringBuilder();
                importFromFile(path);
                if (cur < lastStep.recFirst)
                {
                    // получаем сообщение от первого
                    bytes = first.Receive(data);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    firstRequest = Packet.fromJson(builder.ToString());
                    builder.Clear();
                }
                if (cur < lastStep.recSecond)
                {
                    // получаем сообщение от второго
                    bytes = second.Receive(data);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    secondRequest = Packet.fromJson(builder.ToString());
                    builder.Clear();
                }

                if (cur < lastStep.wasGame)
                {
                    OneRound();
                }

                if (cur < lastStep.sendFirst)
                {
                    // отправляем ответ первому
                    first.Send(Encoding.Unicode.GetBytes(firstResponse.toJson()));
                }

                if (cur < lastStep.sendSecond)
                {
                    // отправляем ответ второму
                    first.Send(Encoding.Unicode.GetBytes(secondResponse.toJson()));
                }
                log("Первый: " + nameState(firstRequest.State) + "; " +
                        "Второй: " + nameState(secondRequest.State) + "; " +
                        "Итог: " + nameState(firstResponse.State) + "-" + nameState(secondResponse.State));
                // вызываем основной процесс игры
                Process();
            }

            // загрузка состояния игры из файла
            private void importFromFile(string path)
            {
                using (var sr = new StreamReader(path))
                {
                    //считываем весь файл в строку
                    string json = @sr.ReadToEnd();
                    //оборачиваем в читающий Json ОБъект
                    JsonTextReader reader = new JsonTextReader(new StringReader(json));
                    //оборачиваем в валидирующий
                    JSchemaValidatingReader validatingReader = new JSchemaValidatingReader(reader);
                    validatingReader.Schema = schema;
                    JsonSerializer serializer = new JsonSerializer();
                    //производим десериализацию
                    init(serializer.Deserialize<Game>(validatingReader));
                }

            }

            // сохранение в файл состояния
            private void exportToFile(string path)
            {
                using (StreamWriter sw = new StreamWriter(path))
                {
                    JsonTextWriter writer = new JsonTextWriter(sw);
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(sw, this);
                }
            }
        }


        static void Main(string[] args)
        {
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(address), port);
            // создаем сокет
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                Console.Write("Восстановить состояние из файла (0-нет/1-да)?> ");
                int f = Console.Read();
                // связываем сокет с локальной точкой, по которой будем принимать данные
                listenSocket.Bind(ipPoint);
                // начинаем прослушивание
                listenSocket.Listen(2);
                Socket first, second;
                log("Сервер запущен. Ожидание подключений...");
                first = listenSocket.Accept();
                log("Первый подключился");
                second = listenSocket.Accept();
                log("Второй подключился");

                Game game = new Game(first, second, "file.txt");

                if (f=='0')
                {
                    game.Process();
                }
                else
                {
                    game.GameAfterImport();
                }
                
                // закрываем сокет
                first.Shutdown(SocketShutdown.Both);
                first.Close();
               
            }
            catch (Exception ex)
            {
                log(ex.Message);
                Console.Read();
            }
        }
    }
}
