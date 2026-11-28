# Zombie Survival Run — пакет графики и анимаций

## Что входит

- FBX Макса, Евы, Walker, Runner, Armored, Brute, Toxic, Crawler и Boss содержат по одному скинированному мешу, UV и 31–33 кости. Топология тела, веса, морфы и обувь адаптированы из ресурсов MakeHuman CC0. Текстиль, экипировка и анимации созданы для проекта. У оружия есть привязка к правой руке и точка вылета.
- В FBX записаны ключевые позы покоя, движения, стрельбы, перезарядки, попадания, атаки и смерти. Шаги рассчитываются по точкам контакта в Blender при создании клипов. События игры выбирают диапазоны готовых клипов.
- Новый атлас, бесшовные цветовая карта и карта нормалей асфальта, панорама первой главы, тёплые отражатели вдоль трассы и холодная заполняющая подсветка, дым и огонь на разбитых машинах. Габариты полос и препятствий сохранены.
- Один общий материал персонажей и один skinned renderer на модель. Импортёр задаёт Android ASTC для новых текстур и отключает несвязанные с игровыми моделями камеры и свет.
- Действующая структура 15 уровней, попадания, здоровье, награды, оружие и ресурсы Арсенала сохранены. Добавлены визуальные предупреждения об атаках, момент нанесения урона остался прежним.

Пакет лежит в `Assets/Resources/ProductionArt`. Unity автоматически импортирует FBX и текстуры при открытии исходного проекта. Пользовательская конфигурация сохраняет приоритет, если она уже назначена. [Сравнение моделей из Blender](../design/character-anatomy-blender.jpg).

## Проверено

[Анатомия и деформации персонажей](CHARACTER_ANATOMY_RU.md).

[Бег героев и походка заражённых](GAIT_AND_INFECTED_RU.md).

[Боевые эффекты, позы предметов и музыка](COMBAT_POLISH_RU.md).

[Сглаживание моделей, город, препятствия, темп и портретный кадр](PORTRAIT_ART_PASS_RU.md).

[Проверка визуальной регрессии и портретный кадр](VISUAL_REGRESSION_RU.md).

Blender 4.3.2 экспортировал и повторно импортировал все девять FBX. Проверка подтвердила иерархию, точки крепления оружия, UV, нормализованные веса вершин, наличие меняющихся поз, 168 кадров у заражённых и 217 кадров у героев. Числа костей, вершин и треугольников записаны в [логе](ART_FBX_ROUNDTRIP.txt).

```sh
blender -b -t 4 --python-exit-code 1 --python Tools/Art/check_characters.py -- Assets/Resources/ProductionArt
```

Редактор Unity 6000.3.28f1 загружен и запущен в batchmode, но завершился с кодом 198 до импорта проекта. Unity Licensing сообщает об отсутствии действительной лицензии. Компиляция всего проекта в Unity, проверка Animator, URP-кадр, Android-сборка и замеры FPS остаются BLOCKED. Статусы проверок из RESULTS_100.md сохранены.

В Unity проверка импорта доступна в меню **Zombie Survival Run 1.0 → Check Shipped Character Pack**. Для журнала CI можно вызвать `ZombieSurvivalRun.EditorTools.ZSRProductionArtValidation.Validate` через `-batchmode -quit -executeMethod`. Эта проверка Unity в данном окружении не запускалась.

## Пересборка ресурсов

```sh
python Tools/Art/build_textures.py --skin-pack /path/to/makehuman_system_assets_cc0.zip
blender -b -t 4 --python-exit-code 1 --python Tools/Art/build_characters.py -- Assets/Resources/ProductionArt \
  --makehuman-data /path/to/makehuman/makehuman/data --skin-pack /path/to/makehuman_system_assets_cc0.zip --preview
```

Генерация текстур требует NumPy и Pillow. Использована ревизия MakeHuman `a8bc2d54ff0ac92e78ff71431b1023eda42bf482`, включая `3dobjs/base.obj`, `targets` и `rigs/default_weights.mhw` внутри `makehuman/data`. Веса имеют SHA-256 `0f3641d651ae3d00ad6b4ccee43142edb109d3bd909d27d9e4139ef1beed8625`. [Системный ZIP CC0](https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip) имеет SHA-256 `b542127a8e25547c7c29c19f2d1d2adb9a664c80396ecd694095dbc8028a0107`. Готовые FBX и карты лежат в Unity-проекте.
