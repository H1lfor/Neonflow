NEONFLOW — LIGHT ROBOT / SOLID ROBOT

Источник дизайна: Neonflow_3D_Model_Briefs, включая оба FULL_RU, общий rig/export
и листы референсов. Готовых моделей в этом пакете не было. Созданы две
самостоятельные упрощённые игровые модели по описаниям, с разной геометрией.

Редактируемый исходник: Assets/Neonflow/Editor/CharacterModelBuilder.cs.
Это Unity-native моделирование; Blender на этом ПК не установлен.
Файлы .blend/FBX не выдаются за выполненную работу. Промежуточные light.glb
и solid.glb содержат настоящую геометрию, скелет, skin, материалы и тестовые позы.
*.mesh.json — исходные данные экспорта, не подмена модели.

Unity prefab: Assets/Neonflow/Resources/Characters/light.prefab, solid.prefab.
У каждого есть действительный Humanoid Avatar, 53 кости, 10 цепочек пальцев
по 3 сустава. Root на полу, +Y вверх, +Z вперёд, масштаб 1, единицы — метры.
Light около 1,80 м; Solid около 1,65 м. Armor rigid-skinned, один вес на вершину.
Модели имеют 6640 и 7072 треугольника: бюджет ниже ориентировочного в brief,
чтобы одновременно работали распознавание камеры и интерфейс игры.
Материалы — MATERIALS.txt; UV/нормали есть. Bitmap-текстуры не требуются
для выбранных однотонных материалов. Только Cyan имеет emission.

Avatar строится по T-позе, prefab сохранён в A-позе. В игре поворот модели 180°
и выбор анатомической руки дают зеркальную подсказку без отрицательного scale.
В игровом кадре корень масштабируется равномерно под композицию интерфейса;
размер самого исходного prefab не меняется. Плечи и пальцы управляются отдельно.

Assets/Neonflow/CharacterTests/{light,solid}: по 23 непустых клипа удержания:
Ready_A, 12 Pose_*, 10 Hand_*. Переходы выполняет игровой RobotView/RobotRig.
Клипы для Idle/Victory/Defeat не имитируются пустыми файлами.
GLB использует glTF 2.0, правую систему координат; при экспорте инвертированы Z,
порядок треугольников, вращения и inverse-bind matrices. Импорт в сторонний DCC
на этом компьютере не проверялся. Реальные модели/риги проверены в Unity.

TestResults/Characters: виды спереди, сзади, сбоку, 3/4; листы 12 поз и 10 жестов.
Порядок плиток — слева направо, сверху вниз; соответствует Movements_catalog.json.
Руки: open, fist, point, peace, three / four, thumb_up, rock, l_shape, point_side.
Позы: arms_up, arms_v, arms_t, elbows_up / cross_chest, clap, reach_left,
reach_right / diagonal_left, diagonal_right, shoulders_up, shoulders_down.
Вершины проверены для 120 сочетаний позы/кисти на каждой модели.

Пересоздание: Neonflow / Build character models. Экспорт и листы:
Neonflow / Export character delivery, затем python Tools/export_characters_glb.py.
Не изменяйте mesh asset вручную без сохранения изменений в исходном генераторе:
повторный запуск генератора заменяет только его собственные assets.

Игровое разделение: Assets/Neonflow/Resources/Data/Characters.json.
Light: arms_up, arms_v, reach_left, reach_right, diagonal_left, diagonal_right;
point, peace, three, l_shape, point_side.
Solid: arms_t, elbows_up, cross_chest, clap, shoulders_up, shoulders_down;
open, fist, four, thumb_up, rock.
Пулы не пересекаются и вместе покрывают все 22 движения. Модели способны
показывать весь каталог; ограничения задаются профилем, а не недостающим ригом.

Манифест входных briefs: 10/11 совпадений; текущий Light FULL_RU отличается от
его SHA256SUMS.txt. Использован прочитанный файл автора, ничего не исправлялось
в исходном пакете. Человеческая приёмка жестов с камерой пока не выполнена.
