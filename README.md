Номер лабораторной работы: 2/3.

Номер варианта: 18.

Описание задания (2): Требуется разработать приложение или программный комплекс, обменивающийся  данными  по  сети  в формате  JSON,  XML  или Protocol Buffers. Сетевая  игра  «Камень,  ножницы,  бумага»  для  двух  игроков. Каждый игрок  в  тайне  от  другого  выбирает  камень,  ножницы либо бумагу, после чего определяется победитель. Камень бьёт ножницы, ножницы бьют бумагу, бумага бьёт камень.

Описание задания (3): Добавьте в свой предыдущий проект возможность сохранения состояния в виде периодического сохранения, либо в виде функций импорта и экспорта. Выбранный формат для сериализации должен иметь  схему.  В  проекте обязателенкод  валидирующийданные. Валидация должна производитьсялибо в программе при импортеданных,   либо   в   юнит-тестах,   проверяющих   корректность сохранения состояния.

Описание алгоритма, который реализован в библиотеке: Сервер ждет подключения двух клиентов, затем начинает игру. Состояние игры хранится в виде объекта с двумя запросами и двумя ответами, а также переменной последнего выполненного шага. Процесс игры: получить ход первого, получить ход второго, произвести раунд, отправить результат первому, отправить результат второму.
Ответ/запрос хранится в виде объекта с одним полем состояния (камень, ножницы, бумага, победа, поражение, ничья, ошибка)

Для третьей лабы реализованы функции : Переподключение клиентов при отвале сервера, загрузка из файла состояния игры, сохранение состояния игры после каждого шага. При загрузке игры происходит валидация с помощью json schema и игра продолжается с последнего выполненного шага.
