# Огонёк: промпты звуков для ElevenLabs

Связанный документ: [ТЗ для кодера по интеграции аудио](audio-integration.md). Здесь храним промпты, имена, Duration, Loop и Prompt Influence; там описаны события, подключение, тесты и критерии приёмки.

Подготовлено 2026-11-14 по локальному checkout MyIndie-Level-11, `docs/concept.md` и внешнему GDD `/Users/fuheshka/Downloads/64Gram Desktop/GDD.md`. Это пакет для генерации и отбора. Готовность сгенерированных файлов проверяем отдельно в `Unity/Assets/Audio/`. Разделение «Звуковик 1/2» сохранено из GDD; закрепление исполнителей за блоками не подтверждено.

## 1. Звуковая концепция и состояние проекта

Игра про маленький живой огонь в огромном холодном мире. Игни звучит близко, мягко и органично: угольки, короткие выдохи огня, сухие искры. Мир звучит холодно и материально: дерево, вода, железо, ветер. Магический звон оставляем чекпоинтам, рождению и финалу. Без мультяшных голосов, аркадных сигналов и постоянного эпического баса.

Основной проект в `Unity/`, веб-прототип `index.html` служит референсом. В коде есть шесть зон, три формы, отдельное топливо, метан, плоты, котёл, вентилятор и финальный костёр. По поиску AudioSource/AudioClip/AudioMixer/PlayOneShot игровое аудио в Unity пока не подключено; AudioListener добавляется билдером камеры. Интро-мультик и меню паузы из GDD в просмотренном GameManager отсутствуют. Для них ниже предусмотрены заготовки.

Важные отличия и уточнения:

1. Маленькая форма не означает низкое топливо. Тревожный `igni_hiss_low` нужен при `fuel < 0.25`, независимо от выбранной формы. Сжатие звучит спокойно.
2. Финал в коде сохраняет Игни: после вспышки он выпрыгивает из костра. Жертвенная передача огня вызывает `Surrender()`, не `Died`: звук обычной смерти здесь не нужен.
3. Верёвка и паутина сейчас сгорают от контакта, рывок не является обязательным условием. Звук привязываем к срабатыванию механики.
4. Лёд-опора восстанавливается. Для его намерзания нужен отдельный звук, которого нет среди SND-01..25.
5. Шкаф, полка и лесное бревно используют мостовую механику, но требуют разных тембров. В текущем Bridge мост появляется сразу: длинный звук падения потребует согласования с будущей анимацией.

## 2. Как генерировать

Открываем [ElevenLabs Sound Effects](https://elevenlabs.io/app/sound-effects). Каждый блок `text` ниже вставляется отдельно, полностью. Duration и Loop задаём в интерфейсе; указанные длительности являются производственными ориентирами, а не обещанием точного тайминга генератора.

Используем доступную модель SFX v3. В [официальной документации](https://elevenlabs.io/docs/overview/capabilities/sound-effects) описаны Duration, Looping и Prompt influence; для полного музыкального сопровождения предусмотрен Music. Промпты описывают слышимое действие и материал. Наименование персонажа и визуальные ссылки не заменяют описание звука.

### Настройка Prompt Influence

Для каждого звука ниже указан **Prompt Influence в процентах интерфейса**. Это выбранные стартовые значения для наших промптов, не измеренные оптимумы и не гарантии результата. Остальные настройки, включая Duration и Loop, задаём отдельно.

| Значение | Где используем | Зачем |
|---|---|---|
| 65% | Просторные атмосферные фоны | Оставляем свободу естественной фактуре при простом описании |
| 70% | Постоянные огненные и воздушные текстуры, финальный костёр | Держим характер и допускаем органичные вариации |
| 75% | Большинство составных событий Игни и окружения | Сохраняем материалы и последовательность без максимальной жёсткости |
| 80% | Предупреждения, короткие направленные действия, точные механические события | Сильнее держим заданное действие и ограничения |
| 85% | Один щелчок, одна капля, очень тихое касание снежинки | Подчёркиваем одиночность и минимальный состав события |

Это принципы выбора, а точное значение берём из карточки конкретного звука. Не ставим 100% автоматически: более высокий Influence сам по себе не означает лучшее качество. Он также не заменяет Loop, монтаж таймингов и проверку отрицательных требований промпта.

При отборе сначала используем записанное значение. Если результат уходит от описания, пробуем +10 процентных пунктов; если теряет естественность, сравниваем с -10. Это пробная корректировка, а не гарантированный способ исправления. Меняем один параметр за раз. При случайной музыке, голосах или лишних событиях также упрощаем описание до нужного слышимого действия.

Для первого прохода делаем по одному кандидату. После выбора тембра Игни делаем 3 отдельных дубля прыжка, рывка, подбора и попадания воды. Каждый дубль генерируем отдельным запросом, не просим несколько вариантов в одном файле.

Для one-shot Loop выключен. Для отмеченных лупов Loop включён: ровная энергия, без вступления и затухания. Шов всё равно проверяем на слух. Если настройки не позволяют короткий клип, берём ближайшую длительность и обрезаем хвост при подготовке.

Сохраняем исходный доступный формат. Для монтажа предпочтителен WAV, если он доступен; преобразование MP3 в WAV не восстанавливает качество. Финальные короткие SFX готовим без стартовой паузы, с сохранённым естественным хвостом. Пространственную реверберацию добавляем при интеграции, чтобы один сухой звук работал в разных комнатах. В промптах ниже она минимальна. Лупы окружения можно оставлять stereo, позиционные события при подготовке проверяем также в mono.

### Куда складывать сгенерированные файлы

Папки подготовлены в основном Unity-проекте:

| Что сгенерировали | Папка от корня репозитория | Пример файла |
|---|---|---|
| SND-01..25, варианты форм и котла, EXT-01..07, UI | `Unity/Assets/Audio/SFX/` | `igni_jump_01.wav` |
| AMB-01..04, фоновые петли зон | `Unity/Assets/Audio/Ambience/` | `house_cold_loop_01.mp3` |
| INTRO-01..04, отдельные слои пролога | `Unity/Assets/Audio/Intro/` | `intro_ember_pulse_01.wav` |

Берём имя из заголовка карточки и добавляем номер дубля: `_01`, `_02`, `_03`. Цифры SND/EXT в имя файла добавлять не нужно. Все варианты сохраняем отдельными файлами, выбранный дубль кодер назначит явно. Расширение оставляем настоящим: скачанный MP3 не переименовываем в WAV. Для лесного варианта SND-14 используем `wind_gust_forest_01` с реальным расширением.

Полный путь к папке: `/Users/fuheshka/Documents/GitHub/MyIndie-Level-11/Unity/Assets/Audio/`. Корневая `Assets/` вне `Unity/` не относится к основному Unity-проекту, аудио туда не кладём. Можно перетащить файлы через Finder или в соответствующую папку окна Project открытого Unity Editor. Уже импортированные файлы перемещаем через Unity вместе с `.meta`, чтобы сохранялись ссылки. Простое копирование ещё не подключает звук к игре: подключение выполняется по [ТЗ для кодера](audio-integration.md).

Приоритеты: **A** сначала, обратная связь игроку и опасности; **B** затем, механики и атмосфера; **C** после них, ещё не подключённые UI/интро и дополнительные детали.

## 3. Звуковик 1: Игни, взаимодействия и UI

### SND-01. igni_crackle_loop

**Prompt Influence: 70%.**

**A · 8 с · Loop on.** Постоянная основа живого Игни. Тихий фон, слышимый в паузах между действиями.

```text
A tiny living ember burning gently at close range, soft irregular charcoal crackles, delicate dry sparks and a faint warm flame flutter. Intimate, fragile and comforting. Steady seamless loop, no large pops, no fade in or out. Isolated fire texture, no room ambience, no music, no voices.
```

### SND-02. igni_hiss_low

**Prompt Influence: 75%.**

**A · 6 с · Loop on.** При критическом топливе. Подмешивать плавно, не включать заново каждый кадр.

```text
A tiny flame struggling to stay alive, thin uneven ember hiss, faint dry sputtering and fragile interrupted flickers. Restrained tension, quiet and intimate, with no sudden loud peaks. Continuous seamless loop at a steady overall level. Isolated fire sound, no human breathing, no heartbeat, no music or ambience.
```

### SND-03. igni_jump

**Prompt Influence: 75%.**

**A · 0.5 с · One-shot.** Только при фактическом прыжке.

```text
A tiny flame springs upward with one soft airy fwip and a delicate dry spark at the start. Quick light attack, warm organic texture, very short clean decay. Small and nimble, not explosive. Single isolated dry game sound, no voice, no melody, no background.
```

### SND-04. igni_dash

**Prompt Influence: 80%.**

**A · 0.6 с · One-shot.** Рывок длится 0.18 с: удар и основное движение должны быть в начале клипа.

```text
A small flame bursts forward in a fast dash: one crisp ignition snap immediately followed by a tight rushing fire whoosh and a few dying sparks. Most energy in the first instant, short dry tail. Sharp and agile, no heavy explosion, no sub bass, no music, no voices or ambience.
```

### SND-05a. igni_transform_big

**Prompt Influence: 75%.**

**A · 0.7 с · One-shot.** Переход к большой форме, основной раздув в первые 0.2 с.

```text
A small flame suddenly swells into a fuller flame, a rounded warm breath of fire expanding upward with soft low crackles. Fast rising body at the start, then a brief natural decay. Confident and organic, no human breath, no explosion, no musical pitch sweep, no background.
```

### SND-05b. igni_transform_small

**Prompt Influence: 80%.**

**A · 0.5 с · One-shot.** Сжатие в фитилёк, без сигнала опасности.

```text
A living flame gently folds inward into a tiny protected ember, one soft inward air flick and a small dry ember tick. Calm, controlled and delicate, quick clean decay. The ember remains alive. No extinguishing hiss, no distressed breathing, no music, no voices or ambience.
```

### SND-05c. igni_transform_normal

**Prompt Influence: 75%.**

**B · 0.5 с · One-shot.** Возврат в обычную форму: нейтральный, слабее большого раздува.

```text
A little living flame settles into a steady medium flame, one gentle warm puff with two or three fine crackles and a short soft tail. Neutral, balanced and reassuring. Single isolated dry effect, no dramatic swell, no human voice, no music or ambience.
```

### SND-06. igni_death

**Prompt Influence: 80%.**

**A · 1 с · One-shot.** Респаун сейчас через 1.2 с. Не растягивать звук на него.

```text
One tiny flame goes out: a short fragile flutter collapses into a fine dry hiss, followed by a single fading ember tick and silence. Small, sad and restrained, all sound decays within one second. No human sigh, no scream, no dramatic bass drop, no music or background.
```

### SND-07. pickup_paper

**Prompt Influence: 75%.**

**A · 0.7 с · One-shot.** Подбор топлива. Бумага коротко вспыхивает, Игни получает подпитку.

```text
A small scrap of brittle old newspaper catches fire instantly: a brief dry paper crinkle, a bright little flame puff and fine crisp sparks. Quick satisfying attack, compact warm decay. Single close isolated effect, no coins, no electronic reward tone, no music, no voices or ambience.
```

### SND-08. candle_light

**Prompt Influence: 75%.**

**A · 1.5 с · One-shot.** Первое зажигание чекпоинта. Не повторять при каждом кадре подзарядки.

```text
A candle wick catches with a delicate soft ignition, followed immediately by one gentle high glass bell chime and a few warm ember crackles. A small comforting moment of safety. Restrained brightness, smooth short ringing decay, no harsh highs. No melody, no voices, no room ambience.
```

### SND-09a. boiler_give_flame

**Prompt Influence: 80%.**

**B · 1 с · One-shot.** Удержание E: нарастание передачи, остановить при отмене. Успех звучит отдельно.

```text
A small stream of flame is drawn into a cold iron furnace, a quiet warm air pull gathering into a low resonant heat hum. A smooth one-second rise that remains unresolved, no final impact or ignition. Close isolated mechanical fire texture, no music, no voices or room ambience.
```

### SND-09b. boiler_ignite

**Prompt Influence: 75%.**

**B · 2.5 с · One-shot.** Успешный запуск котла, после завершения удержания.

```text
An old cast iron boiler wakes up: a deep hollow ignition thump, a rounded furnace flare, then a short rising pressure hiss and a few hot metal ticks settling down. Heavy but contained, not a blast. Isolated dry machinery effect, no alarm, no music, no voices or background.
```

### SND-10. bonfire_ignite

**Prompt Influence: 70%.**

**A · 4 с · One-shot.** Начало точно на вспышке костра. Секунду тишины перед ним делаем в игре, не внутри файла.

```text
A large ritual bonfire ignites in one powerful rounded rush of warm flame, deep wooden fire resonance opening outward into a bright shower of fine sparkling embers. Awe and relief, then a natural settling fire tail. No detonation, no destructive debris, no brass hit, no choir, no music or voices.
```

### SND-11. ui_pause_open

**Prompt Influence: 80%.**

**C · 0.6 с · One-shot.** Пауза пока не обнаружена в коде. Low-pass мира делается микшером, не этим клипом.

```text
One soft low rounded charcoal thump with a brief muffled air cushion, close and subdued, fading quickly into silence. Tactile dark fairytale menu transition. No sharp attack, no huge cinematic bass, no water splash, no music, no voices or background.
```

### SND-12. ui_click

**Prompt Influence: 85%.**

**C · 0.5 с · One-shot, полезный звук около 0.1 с.** Обрезать лишнюю тишину. Для hover можно использовать более тихий дубль.

```text
One tiny dry ember tick, soft and tactile like a small charcoal flake gently snapping. Immediate attack, extremely short decay, intimate and understated. Isolated menu click, no ignition burst, no metallic ring, no electronic beep, no music, no voices or ambience.
```

## 4. Звуковик 2: окружение и опасности

### SND-13. wind_warning

**Prompt Influence: 80%.**

**A · 1 с · One-shot.** В Fan предупреждение длится 1 с перед порывом. Сигнал должен различаться с атмосферным ветром.

```text
Cold air starts squeezing through a narrow wooden window crack, a focused hollow whistle rising in pressure over one second. A clear restrained warning, tense but not piercing, ending just before the gust. Isolated air sound, no impact, no curtain, no music or voices.
```

### SND-14. wind_gust

**Prompt Influence: 75%.**

**A · 1.6 с · One-shot.** Длина активного порыва в Fan около 1.6 с. Для леса нужен вариант без рамы.

```text
One forceful gust of winter air blasts through a broken wooden window, broad rushing wind with a brief dry frame rattle. Fast clear onset, sustained pressure, then a short release. Strong but not thunderous. No fabric, no howling creatures, no music, no voices or other ambience.
```

### SND-15. curtain_flap

**Prompt Influence: 80%.**

**B · 1.6 с · One-shot.** Отдельный слой ткани поверх порыва, без второго ветра.

```text
A worn heavy cotton curtain suddenly billows and flaps in a strong draft, rough fabric pulling taut then fluttering in several soft irregular folds. Close dry cloth foley with a short settling tail. Fabric only, no wind recording, no window rattle, no music, no voices or ambience.
```

### SND-16. drop_fall_water

**Prompt Influence: 85%.**

**A · 0.7 с · One-shot.** Одна капля, 3 дубля на отбор. Эхо подвала добавляем отдельно.

```text
One distinct water droplet falls into a shallow cold puddle, a clear small plip with a tiny hollow water resonance and delicate ripple decay. Close detailed foley, crisp but not piercing. Single dry isolated drop, no dripping sequence, no running water, no long echo, no music or voices.
```

### SND-17. drop_hit_igni

**Prompt Influence: 80%.**

**A · 0.6 с · One-shot.** Урон от капли. Должен быть резче безопасного всплеска.

```text
A cold water droplet hits a small hot ember, one sharp tight steam hiss with a brittle sputtering fire crackle and a tiny spit. Immediate stinging attack, fast dry decay. Small-scale thermal contact, no full extinguishing, no explosion, no voice, no music or background.
```

### SND-18. ice_melt

**Prompt Influence: 75%.**

**A · 1.2 с · One-shot.** Треск, пар, разрушение льда. При интеграции привязать к Melt(), без повторного запуска на каждом Tick.

```text
A small thick block of ice rapidly gives way under heat, close brittle internal cracks followed by a short fine steam hiss and a few light water drips. Clear compact fracture at the start, then a natural wet decay. No avalanche, no giant glass crash, no music, no voices or ambience.
```

### SND-19. rope_burn

**Prompt Influence: 80%.**

**B · 0.6 с · One-shot.** В коде звено прогорает за 0.6 с. Для цепочки не складывать десятки громких копий.

```text
A short piece of old hemp rope burns through, fine dry fibrous crackles gathering into one crisp little strand snap at the end. Brief intimate close-up fire foley, rough natural fibers. No large flames, no gunshot, no music, no voices or room ambience.
```

### SND-20. wardrobe_crash

**Prompt Influence: 75%.**

**B · 1.5 с · One-shot.** Сейчас мост появляется сразу. Начальный удар ставим на появление, без длинного предварительного падения.

```text
A heavy old wooden wardrobe lands flat on timber floor with one deep hollow wooden crash, a brief scattering of small mirror fragments and creaking joints settling. Impact right at the start, short debris tail. Weighty and believable, no explosion, no music, no voices or room ambience.
```

### SND-21. hatch_slam

**Prompt Influence: 80%.**

**B · 1 с · One-shot.** Закрытие люка после падения. Открытие требует другого события, не запускаем этот хлопок на оба действия.

```text
A thick wooden floor hatch shuts once, a short iron latch clack followed closely by a solid muffled timber thud and a brief hinge rattle. Close heavy tactile foley, quick natural decay. No prison door slam, no long echo, no music, no voices or background.
```

### SND-22. cobweb_burn

**Prompt Influence: 80%.**

**B · 0.5 с · One-shot.** Тише верёвки и подбора.

```text
A delicate sheet of dry cobweb instantly shrivels in a small flame, a very soft papery fizz and fine thread crackles collapsing into silence. Light, fragile and extremely brief. Isolated dry close-up texture, no flame roar, no insect sounds, no music, no voices or background.
```

### SND-23. raft_creak

**Prompt Influence: 75%.**

**B · 2 с · One-shot.** При посадке на плот или редким событием рядом. Течение держим отдельным фоном.

```text
A small wet wooden raft flexes under a light landing, a low damp board creak followed by two soft close water laps against its edge. Restrained rough timber detail, short natural settling tail. No boat engine, no constant river bed, no voices, no music or distant ambience.
```

### SND-24. methane_pop

**Prompt Influence: 80%.**

**A · 0.8 с · One-shot.** Подпитка и толчок, не сигнал урона. Большой форме можно дать более плотный вариант того же тембра.

```text
One wet gas bubble pops and immediately catches fire, a rounded hollow bloop followed by a compact warm flame whoomp and a few bright sparks. Springy upward energy, satisfying and buoyant. Small contained ignition, no bomb blast, no harsh steam damage hiss, no music, no voices or background.
```

### SND-25. fan_heavy_loop

**Prompt Influence: 70%.**

**B · 8 с · Loop on.** Лопасти ShaftFan, не оконный Fan. Остановка по сгоревшему ремню.

```text
A large old ventilation fan rotating steadily, a low mechanical motor hum beneath broad slow blade swishes and a faint worn bearing vibration. Heavy moving air, stable speed and volume. Seamless continuous loop with no startup or shutdown. Isolated machinery, no alarms, no music, no voices or room echo.
```

## 5. Дополнения по фактическим механикам

Эти звуки не входят в исходные 25 позиций. Это предложенное расширение, при нехватке времени сначала делаем основные A.

### EXT-01. ice_refreeze

**Prompt Influence: 70%.**

**B · 1 с · One-shot.** Во время видимого намерзания: через 3 с после разрушения, если Игни не мешает. Восстановление при респауне можно делать бесшумно.

```text
A thin sheet of frost rapidly grows into solid ice, delicate crystalline ticks spreading outward with a soft fine icy creak, ending in a small firm settling click. Quiet cold natural texture over one second. No magic melody, no glass breaking, no music, no voices or ambience.
```

### EXT-02. updraft_loop

**Prompt Influence: 70%.**

**B · 6 с · Loop on.** Тёплый восходящий поток: мягче и ниже опасного сквозняка.

```text
A steady column of warm air rising from a pipe, soft rounded upward rushing air with a gentle low airy resonance. Smooth, supportive and calm, no sharp whistle. Continuous seamless loop at constant strength, no start or finish. No fan motor, no music, no voices or room ambience.
```

### EXT-03. fan_stop

**Prompt Influence: 80%.**

**B · 2 с · One-shot.** После пережигания ремня. Хвост подгоняем под визуальный доворот, он сейчас не фиксирован по длительности.

```text
A heavy old ventilation fan loses its drive belt, a brief dry belt snap followed by blade swishes slowing down and a tired metal bearing creak settling to rest. Compact natural mechanical shutdown. No violent crash, no alarm, no music, no voices or room ambience.
```

### EXT-04. shelf_drop

**Prompt Influence: 80%.**

**B · 1 с · One-shot.** Для подвальных полок без зеркального стекла шкафа.

```text
A thick wooden cellar shelf drops flat onto stone supports, one firm hollow plank clack followed by a short rough timber creak and a few dust-like tiny debris ticks. Impact at the start, close dry foley. No glass, no barrel rolling, no music, no voices or ambience.
```

### EXT-05. log_drop

**Prompt Influence: 80%.**

**B · 1.5 с · One-shot.** Для лесного бревна-моста.

```text
A heavy fallen pine trunk lands across two snowy banks, one deep solid wooden thud with a brief brittle twig snap and a soft powdery snow scatter. Impact at the start, short natural tail. No furniture, no glass, no explosion, no music, no voices or background wind.
```

### EXT-06. igni_respawn

**Prompt Influence: 75%.**

**B · 0.8 с · One-shot.** Короткое возвращение жизни после Revive(), можно также взять для рождения в интро.

```text
A tiny ember rekindles into a living flame, a delicate bright ignition flick followed by a soft warm flutter and a few fine dry sparks. Small, hopeful and intimate, short clean decay. No triumph fanfare, no bell melody, no voice, no music or ambience.
```

### EXT-07. snow_hit_igni

**Prompt Influence: 85%.**

**B · 0.5 с · One-shot.** Мягкий вариант водного урона для снежинок. При интеграции ограничить частоту, не озвучивать каждую снежинку.

```text
A tiny snowflake touches a warm ember and vanishes with one very soft fine steam tick and a faint short hiss. Delicate, close and almost weightless, much gentler than a water splash. Single dry isolated effect, no crunchy footstep, no music, no voice or background.
```

## 6. Фоны зон

Все четыре фона: **B · 12 с · Loop on**. Это отдельные атмосферные текстуры без музыкального дрона. Они не должны скрывать предупреждение ветра, капли и критическое топливо.

### AMB-01. house_cold_loop

**Prompt Influence: 65%.**

```text
Inside a large abandoned wooden house on a freezing night, faint distant winter air leaking through cracks and very sparse quiet timber settling creaks. Cold enclosed stillness with plenty of empty space, subdued stable sound bed. Seamless loop, no dramatic gusts, no fire, no rhythmic dripping, no music or voices.
```

### AMB-02. sewer_water_loop

**Prompt Influence: 65%.**

Для подвала можно сначала обойтись локальными каплями и тихим домовым фоном. Этот водный слой нужен именно канализации.

```text
A slow dark underground water channel, subdued continuous water movement with soft irregular laps against stone edges. Close low flowing water, enclosed but with only a short natural reflection. Stable seamless ambience loop, no distinct loud drops, no gas pops, no rats, no music, no voices or tonal drone.
```

### AMB-03. shaft_air_loop

**Prompt Influence: 70%.**

Механический вентилятор SND-25 накладывается отдельно и может остановиться, воздух остаётся.

```text
Air moving slowly through a tall old ventilation shaft, low soft hollow air turbulence and faint distant metal structure resonance. Sparse industrial atmosphere with stable restrained level. Seamless loop, no motor, no rotating blades, no rhythmic bangs, no music, no voices or melodic drone.
```

### AMB-04. forest_winter_loop

**Prompt Influence: 65%.**

```text
A deserted pine forest on a deep winter night, soft broad cold wind moving through distant needles with very faint dry branch movement. Open snowy space, lonely and restrained, steady seamless loop. No sudden gust warnings, no footsteps, no birds, no wolves, no music, no voices or tonal drone.
```

Для порыва в лесу используем SND-14 с тем же **Prompt Influence 75%**, Duration 1.6 с и Loop off, с заменой первого предложения на: `One forceful gust of winter wind sweeps between pine trunks, broad rushing cold air with a light dry hiss of blown powder snow.` Остальной промпт сохраняем, убираем упоминание рамы. В зоне костра лесной фон постепенно уходит в тишину.

## 7. Звуки пролога

Это план звукового монтажа по внешнему GDD, не задача на генерацию видео. Монтаж 35-40 с собираем слоями. Не просим один генератор угадать все таймкоды. Реплик и диктора нет. Музыкальная шкатулка/виолончель относятся к отдельному музыкальному треку.

| Время | Кадр | Звук |
|---|---|---|
| 0:00-0:10 | Ночная долина, люди несут огонь | INTRO-01 метель; факелы можно тихо взять из SND-01. Без обязательного гула толпы |
| 0:10-0:18 | Камера подходит к заброшенному дому | Метель приглушаем, переходим к AMB-01; музыкальную тему ведём отдельно |
| 0:18-0:26 | Рисунок у камина | INTRO-02 бумага в сквозняке, очень тихий воздух дома |
| 0:26-0:34 | Свет под золой, рождение Игни | INTRO-03 пульс; затем EXT-06 и INTRO-04, вводим SND-01 |
| 0:34-0:40 | Игни оживает, старт игры | Убираем пульс, мягкий одиночный звон из SND-08 при передаче управления |

### INTRO-01. intro_blizzard

**Prompt Influence: 65%.**

**C · 12 с · Loop on.** Более дальняя и широкая метель, чем опасные порывы в геймплее.

```text
A wide distant winter blizzard crossing a snow-covered valley at night, layered cold air rushing softly across open ground and a faint fine hiss of airborne snow. Broad lonely cinematic outdoor atmosphere, steady seamless loop. No thunder, no wolves, no footsteps, no voices, no music or tonal drone.
```

### INTRO-02. intro_drawing_rustle

**Prompt Influence: 80%.**

**C · 3 с · One-shot.** Бумага остаётся приколотой, не звук переворачиваемых страниц.

```text
A single old paper drawing pinned to a wall gently trembles in a faint draft, several delicate irregular dry paper flutters and a tiny pin contact tick. Fragile close-up foley, very quiet, natural short settling tail. No page turning, no wind recording, no music, no voices or room ambience.
```

### INTRO-03. intro_ember_pulse

**Prompt Influence: 75%.**

**C · 4 с · Loop on.** Низкий органичный пульс под золой. Громкость и ускорение при необходимости монтируем отдельно.

```text
A quiet slow heartbeat-like double pulse made from soft rounded ember thumps and faint charcoal movement, intimate and low, as if warmth is waking under ash. Gentle regular pairs with breathing space between them, seamless loop. No medical monitor, no literal breathing, no horror stinger, no music or voices.
```

### INTRO-04. intro_ash_scatter

**Prompt Influence: 75%.**

**C · 1.5 с · One-shot.** Отдельный слой рождения, не вшитый в постоянный треск Игни.

```text
A tiny warm puff lifts a handful of cold fireplace ash, soft dry powder scattering outward with a few fine brittle charcoal grains and delicate bright sparks settling. Close detailed light foley, short natural tail. No cough, no explosion, no large bonfire, no music, no voices or background.
```

## 8. Порядок работы и проверка

1. Генерируем SND-01, SND-03, SND-04, SND-06 и SND-07: выбираем общий тембр Игни на пяти разных действиях.
2. Генерируем оставшиеся A: слабое топливо, формы, чекпоинт, предупреждение и порыв ветра, капля, урон, лёд, метан, костёр.
3. Генерируем B для механик, которые встречаются в текущем уровне.
4. Генерируем атмосферные лупы.
5. Генерируем C после готовности монтажа интро и UI.
6. Прослушиваем выбранные клипы подряд и в повторении; отбрасываем случайные голоса, мелодии, лишний фон, перегруз и длинную тишину перед действием.
7. Проверяем петли минимум пятью повторами: нет щелчка, провала уровня и узнаваемого громкого всплеска на каждом цикле.
8. Проверяем в игре различимость: подбор / урон, маленькая форма / критическое топливо, тёплый поток / опасный ветер, смерть / финал.

При подключении постоянный треск меняем плавно по форме; тревожный слой отдельно по топливу. Для перехода ниже/выше порога критического топлива потребуется сглаживание, чтобы петля не дёргалась от подпитки. Повторяющиеся события запускаем по факту действия, не по состоянию каждого кадра. Снежинки, соседние горящие звенья верёвки и несколько плотов требуют ограничения одновременных голосов. Громкие события слушаем вместе с фонами, оставляем запас до клиппинга.

Финал синхронизируем по Bonfire: от момента успешной передачи огня затухание до 1.6 с, тишина 1.6-2.6 с, SND-10 на 2.6 с, возвращение Игни на 4.2 с. Музыка и реверберационные хвосты тоже должны освободить секунду тишины. Шум котла не вшиваем в удержание E: игрок может его прервать.

Не проверено: реальная генерация ElevenLabs, качество на слух, бесшовность петель, громкость в игровом миксе, подключение событий, runtime-поведение Unity и соответствие checkout последней версии origin. Риск: генератор может добавить лишние слои или сместить атаку; тайминг и громкость потребуют отбора и монтажа.
