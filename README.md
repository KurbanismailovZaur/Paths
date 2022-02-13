# Paths
Paths - это написанная на чистом C# библиотека для создания и редактирования waypoint системы в редакторе Unity и ее применении во времени выполнения игры. Paths использует алгоритм построения кривых линий Кэтмулл-Рома.

# Поддерживаемые версии Unity
Paths поддерживает любую версию Unity, в которой имеется поддержка новой системы пользовательского интерфейса UI Elements.

# Установка
Все что вам надо сделать это импортировать unity-пакет в свой проект. Технически библиотека Paths это всего лишь папка Plugins/Numba/Paths с несколькими скриптами и прочими файлами.

![Снимок экрана 2022-02-12 015235](https://user-images.githubusercontent.com/5365111/153681466-9dd83da4-e140-4e70-96ef-b31f6cf302e1.png)

# Создание путей в редакторе
Чтобы создать путь в редакторе во вкладке иерархии вызовите контекстное меню и выберите `Path` > `Empty`.

![image](https://user-images.githubusercontent.com/5365111/153685315-3fb5a081-6824-44b3-a619-1da687b1116e.png)

Это создаст объект пустышку и добавит к нему скрипт `Path`. Конечно, вы можете создать пустышку и добавить скрипт `Path` через меню компонентов вручную, однако готовое меню для этого удобней. Помимо пустого пути вы можете создавать уже готовые (линию, треугольник и прочие) но к ним мы вернемся чуть позже.

# Настройка пути в редакторе

![image](https://user-images.githubusercontent.com/5365111/153685666-8199776b-3370-4ea2-9e4a-e7e364ee1d16.png)

Скрипт состоит из нескольких блоков. В первую очередь нас интересует блок точек (желтый), сейчас он пустой, потому как путь по умолчанию не содержит ни одной точки. Чтобы добавить точку, нажмите на значек `+` в нижнем правом углу блока.

![image](https://user-images.githubusercontent.com/5365111/153688450-ae1b4111-bb65-4e0f-a559-f62ffe189704.png)

Это добавит одну точку в путь.

![image](https://user-images.githubusercontent.com/5365111/153688478-89de4df6-0b94-483e-bf60-d1108a5d052b.png)

Как видите теперь в пути есть одна точка. Желтый номер в левой части - это позиция точки во всем списке. Далее расположены два `Vector3` поля. Верхнее поле это позиция точки, а нижнее - поворот в виде углов Эйлера. Позиция и поворот точки расчитываются относительно игрового объекта к которому прикреплен скрипт `Path`. Поэтому сейчас точка 0 совпадает с позицией родительского объекта. Это также заметно в окне сцены.

![image](https://user-images.githubusercontent.com/5365111/153688932-b5dd0650-15ac-4470-9a82-c5baf5dee3a4.png)

Текст `pivot` указывает на позицию родительского объекта. Измените позицию точки в инспекторе на [1, 0, 0] и переключите камеру окна сцена на вид сверху, чтобы было проще наблюдать. Вы увидите что точка и пивот объекта теперь в разных местах.

![image](https://user-images.githubusercontent.com/5365111/153689117-4ec00285-84a5-4275-93c1-abc3d41ead80.png)

Перемещая, вращая и масштабируя объект к которому прикреплен скрипт `Path` вы также перемещаете и все его точки. Мыслите о точках так, словно они являются дочерними по отношению к объекту к которому прикреплен скрипт.

Теперь добавьте еще одну точку, для этоно нажмите кнопку `+` около точки в окне инспектора.

![image](https://user-images.githubusercontent.com/5365111/153689293-378724e5-ff04-42d5-a48b-3c48b9a05ddb.png)

Это создаст еще одну точку сразу после текущей. Установите ее на позицию [2, 0, 0]. Теперь наш путь выглядит так.

![image](https://user-images.githubusercontent.com/5365111/153689827-6b6aba7b-ae19-4ed2-a591-6070f78ce809.png)


![image](https://user-images.githubusercontent.com/5365111/153689368-6d743e0f-5051-4c63-a12a-78fc4f482fc4.png)

Желтая линия между двумя точками - это линия пути. Позже мы разберемся как сделать так, чтобы по этой линии двигался объект. Для удаления точки вы можете щелкнуть на кнопку `-` в окне инспектора напротив нужной вам точки.

Чтобы увидеть номер точки в окне сцены просто наведите курсор на нужную точку.

![Анимация](https://user-images.githubusercontent.com/5365111/153711348-8571e2de-0238-4826-9b00-b9bb5bb929c7.gif)

Как видите ориентироваться с точками в окне сцены не сложно. Вы также можете добавить новую точку прямо в окне сцены. Для этого нажмите на синию кнопку `+`. Это создаст в этом месте новую точку и сцепит ее с предыдущей.

![Анимация](https://user-images.githubusercontent.com/5365111/153711404-50486301-9e77-40b1-a12b-df3b1fd77975.gif)

Теперь путь выглядит немного непонятно, давайте переместим точку 1 наверх. Вы можете сделать это прямо в окне сцены, для этого щелкните по точке 1 чтобы выделить ее, включите инструмент перемещения (горячая клавиша W) и переместите точку вверх на позицию 1 по оси z (используйте CTRL для привязки).

![Анимация](https://user-images.githubusercontent.com/5365111/153711478-4ec60731-3d82-4de0-a601-89fe72e3a299.gif)

Когда какая-либо точка выделена, в нижней правой части окна сцены появляется блок быстрого редактирования этой точки.

![image](https://user-images.githubusercontent.com/5365111/153690277-9edc8aed-b1a6-4e51-a980-06320e633e7a.png)

Этот блок по сути просто дублирует поля точки, которые отображаются в окне инспектора. Вы можете использовать его, а можете просто перетягивать точку в нужное место используя гизмо инструменты.

Можно добавлять новые точку между уже существующими, для этого наведите курсор на маленький белый круг между двумя желтыми точками, появится синяя кнопка `+`, нажмите на нее чтобы добавить точку.

![Анимация](https://user-images.githubusercontent.com/5365111/153711633-97db945b-2cb3-4d99-8320-9da2e1648ba8.gif)

Теперь путь состоит из 4 точек. О том в чем разница между белыми точками и желтыми, а также между пунктирной линией и желтой поговорим чуть позже, сейчас надо просто научиться редактировать линии.

Вы также можете удалять не выделенные точки прямо в окне сцены, для этого наведите курсор чуть выше самой точки, появится кнопка удаления точки со знаком `x`, <b>дважды</b> щелкнув по ней вы удалите точку.

![Анимация](https://user-images.githubusercontent.com/5365111/153711702-13261a59-5607-4069-8ffd-4b79e1d49e6b.gif)

Отмените последне действие (удаление точки) нажав CTRL + Z. `Paths` полностью поддерживает систему отмены/повтора действий встроенную в Unity.

Работать с точками в окне сцены довольно комфортно, однако некоторые параметры настраиваются только в инспекторе. Первый блок скрипта `Path` в инспекторе имеет 2 поля: `Resolution` и `Looped`.

`Resolution` - разрешение пути, то есть этот параметр отвечает за то сколько прямых линий надо создавать между двумя точками. Когда `Resolution` равен 1, то между двумя точками рисуется одна линия, таким образом весь пути представляет собой ломанную линию. Увеличение значения разрешения сделает линию кривой. Чем выше разрешение, тем более сглаженной становится линия.

![Анимация](https://user-images.githubusercontent.com/5365111/153711869-76eb0441-5f23-412f-b9f4-0a919ddcb059.gif)

Второе поле - `Looped`, отвечает за зацикленность пути.

![Анимация](https://user-images.githubusercontent.com/5365111/153712020-503390df-3acf-4e1b-bb0d-5f61d5d49858.gif)

Вы можете найти оптимальное разрешение для пути нажав на кнопку `Optimize` под полем `Resolution`.

![Анимация](https://user-images.githubusercontent.com/5365111/153728274-5de834d5-33bd-4513-8df1-854f57a5931d.gif)

Сразу под полем `Looped` находится информация о том какой длины получается проходимый путь (желтые линии).

Вы можете менять местами точки пути.

![Анимация](https://user-images.githubusercontent.com/5365111/153712175-c1069643-892a-476f-a6e9-ba220b45162f.gif)

Когда путь полностью настроен, вы можете посмотреть как по нему будет двигаться объект. Для этого раскройте блок отладки (зеленый), в сцене появятся зеленые грани воображаемого куба, который будет находится в точке 0. Вы можете двигать слайдер `Position` чтобы указать на каком проценте пути вы хотите чтобы был расположен этот куб.

![Анимация](https://user-images.githubusercontent.com/5365111/153712337-4ec402a1-ac6e-4fea-a59b-af8f0dc7cba9.gif)

По умолчанию при отладке лицевая часть куба всегда направлена в соответствии с вектором движения по пути.

![Анимация](https://user-images.githubusercontent.com/5365111/153712454-033c6449-50cb-4e5c-b937-d6dd52532cc6.gif)

Однако вы можете изменит это поведение отключив поле `Use Path Direction`. В таком случае куб будет соответствовать поворотам точек. В нашем примере мы не меняли повороты точек (только их позиции), поэтому повороты всех точек совпадают с глобальными осями.

![Анимация](https://user-images.githubusercontent.com/5365111/153712556-4536672a-1f01-4556-9803-b5dd226ab5c1.gif)

Для работы с поворотами точек включите инструменты `Вращение` (горячая клавиша E). Вы можете выставить нужные повороты точек в списке точек в инспекторе.

![Анимация](https://user-images.githubusercontent.com/5365111/153712646-53b180d4-af42-410d-ba00-bbc2ea529518.gif)

Либо вращать точку с помощью гизмо в окне сцены.

![Анимация](https://user-images.githubusercontent.com/5365111/153712685-6efdb3f4-251c-4886-97a5-70a01d39f7e6.gif)

Чтобы легче было понимать поворот точки, внутри гизмо инструмента `Вращение` отображаются локальные оси точки. После того как все нужные точки повернуты вы можете отладить путь. При прохождении по точками поворот куба будет соответствовать поворотам этих точек.

![Анимация](https://user-images.githubusercontent.com/5365111/153712804-f524a98c-b5f5-4c25-b359-37e3f4497c5d.gif)

# Создание готовых шаблонов путей

Помимо пустого пути вы можете создавать пути с уже существующими в них точками. Для этого используйте контекстное меню `Path` > `...` в окне иерархии. Например, если вы хотите создать круг, то используйте `Path` > `Circle`.

![Анимация](https://user-images.githubusercontent.com/5365111/153728474-61701a8e-5d4e-41f6-af56-db82294fb3fe.gif)

Или к примеру вы хотите 3D-спираль.

![Анимация](https://user-images.githubusercontent.com/5365111/153728561-cbd99b0a-cdad-42b8-bb1c-4905597e1bd5.gif)

# Частные случаи пути
Теперь, когда вы знаете как управлять линиями в редакторе, надо разобраться с частными случаями построения пути, а именно, когда путь состоит из 0, 1, 2, 3 и более чем 3 точек.

## Путь из 0 точек.
В этом случае путь - это пустышка. Вызов методов для получения данных (о них чуть позже поговорим) на пути будет приводить к исключению.

## Путь из 1 точки
Путь из 1 точки будет всегда возвращать значение в этой точке при вызове методов получения данных на пути, причем неважно путь зациклен или нет.

![image](https://user-images.githubusercontent.com/5365111/153750540-c06ebac6-1505-4fc8-8103-5e3b91e0fba6.png)

## Путь из 2 точек
Алгоритм Кэтмулл-Рома для 2 точек может построить только прямую линию, поэтому независимо от значения поля `Resolution` между двумя точками будет всегда прямая линия. Если путь зациклен, то он будет состоять из двух сегментов, первый от точки 0 к точке 1, а второнй - наоборот.

![image](https://user-images.githubusercontent.com/5365111/153750549-b70cb34f-c045-42d3-8f8b-3fb260a1d481.png)

## Путь из 3 точек
Три точки позволяют описать один сегмент кривой линии. Точка 0 в этом случае является контроллирующей для точек 1 и 2, а точки 1 и 2 - конечными, то есть теми между которых рисуется путь. Поле `Resolution` влияет на сглаженность это линии.

![Анимация](https://user-images.githubusercontent.com/5365111/153750700-d6534df8-02e4-432e-a269-114493e0e008.gif)

При этом путь может быть зацикленным.

![Анимация](https://user-images.githubusercontent.com/5365111/153750812-fc1a34d7-f7b7-48a3-98ff-aabe4a0c10e6.gif)

# Путь из более чем 3 точек
Когда путь состоит из более чем 3 точек, то первая точка (с индексом 0) и последняя являются контроллирующими, а все остальные - конечными. Вы можете отдельно контролировать линию исходящую из точки 1 благодаря контроллирующей точке 0. То же самое и с предпоследней точкой. Контроллирующие точки рисуются белым цветом, а их связь - белой пунктирной линией. Эти точки не участвуют в проходимом пути, а лишь вляют на него.

![Анимация](https://user-images.githubusercontent.com/5365111/153751542-b05c03c8-2767-48b5-990f-f5b8b33a81d5.gif)

А вот так может выглядеть путь из 7 точек.

![Анимация](https://user-images.githubusercontent.com/5365111/153751688-05087dbf-e8b8-4745-a1a5-6c8c41ffdc34.gif)

# API
Для работы с путями через код было разработано удобное для этого API.

## Создание пути
Чтобы создать путь используйте статический метод `Path.Create()`. Он создаст игровой объект с именем "Path" в нулевой позиции, добавит компонент `Path` к нему и вернет его как результат. Данный метод имеет 4 перегрузки:
1. `Create()`
2. `Create(Vector3 pivotPosition)`
3. `Create(Vector3 pivotPosition, bool useGlobal, params Vector3[] points)`
4. `Create(Vector3 pivotPosition, bool useGlobal, IEnumerable<Vector3> points)`

Где:
* `pivotPosition` - позиция пути.
* `useGlobal` - указывает передаются-ли точки (параметр `points`) в глобальном (`true`) пространстве или локальном (`false`).  
* `points` - коллекция точек.

К примеру

`var path = Path.Create(new Vector3(1f, 0f, 1f), true, Vector3.zero, new Vector3(1f, 0f, 0f));` 

создаст новый путь расположенный на позиции [1, 0, 1] с двумя точками на глобальных позициях [0, 0, 0] и [1, 0, 0].

![image](https://user-images.githubusercontent.com/5365111/153730736-846724a8-d2ac-4251-abce-ff301618465d.png)

Вы также можете использовать методы `CreatePolygon` и `CreateSpiral`.

`CreatePolygon` создает путь-многогранник (треугольник, ромб, пятиугольник и прочие). Имеются 3 перегрузки:
1. `CreatePolygon(int sideCount, float radius)`
2. `CreatePolygon(Vector3 pivotPosition, int sideCount, float radius)`
3. `CreatePolygon(Vector3 pivotPosition, Vector3 normal, int sideCount, float radius)`

Где:
* `sideCount` - количество граней.
* `radius` - расстояние от центра фигуры до любого угла.
* `pivotPosition` - позиция пути в пространстве.
* `normal` - нормаль фигуры, то есть вектор представляющий куда направлена лицевая сторона фигуры в пространстве.

К примеру

`var path = Path.CreatePolygon(5, 1f);`

этот код создаст пятиугольник с радиусом 1 метр.

![image](https://user-images.githubusercontent.com/5365111/153734111-973d5c08-c279-4a5f-b13d-a2a1bed63ad6.png)

`CreateSpiral` создает путь-спираль (Архимедову). Имеются 3 перегрузки:
1. `CreateSpiral(float offsetAngle, int coils, float step, int pointsCountPerCoil, bool use3D = false)`
2. `CreateSpiral(Vector3 pivotPosition, float offsetAngle, int coils, float step, int pointsCountPerCoil, bool use3D = false)`
3. `CreateSpiral(Vector3 pivotPosition, Vector3 normal, float offsetAngle, int coils, float step, int pointsCountPerCoil, bool use3D = false)`

Где:
* `offsetAngle` - угловое смещение (в градусах) спирали, то есть поворот спирали вокруг ее центра.
* `coils` - количество витков спирали.
* `step` - шаг спирали, то есть расстояние между двумя витками.
* `pointsCountPerCoil` - количество генерируемых точек на один виток.
* `use3D` - нужно-ли создавать трезмерную спираль?
* `pivotPosition` - позиция пути в пространстве.
* `normal` - нормаль фигуры, то есть вектор представляющий куда направлена лицевая сторона фигуры в пространстве.

К примеру

`var path = Path.CreateSpiral(0f, 3, 1f, 8, true);`

этот код создаст 3D-спираль со смещением 0 градусов, 3 витками, расстоянием между витками 1 метр и 8 точками на виток.

![Анимация](https://user-images.githubusercontent.com/5365111/153734351-d5924f63-68ed-48f0-b492-14602f0170e6.gif)
