# Guía de audio de Mishra
Para quien pone los sonidos. No hace falta saber programar. Hecha para **Unity 6.3 LTS (6000.3.11f1)**.

Todo se hace arrastrando archivos de audio a casilleros que se ven en pantalla. Si algo de esta guía no coincide con lo que ves, frená y preguntá antes de seguir.

---

## A. Antes de empezar: las ventanas de Unity

Unity tiene varias ventanas. En esta guía se nombran así:

| Nombre | Qué es | Dónde está normalmente |
|---|---|---|
| **Project** | Todas las carpetas y archivos del juego (audios, escenas, prefabs) | Abajo |
| **Hierarchy** | La lista de objetos de la escena abierta | A la izquierda |
| **Inspector** | Las propiedades del objeto o archivo que seleccionaste | A la derecha |
| **Console** | Los mensajes y errores del juego | Pestaña al lado de Project |
| **Scene / Game** | La vista del mapa y la vista del juego | Al centro |

Palabras que vas a ver:
- **Escena:** un mapa o pantalla del juego (el menú, la selección de personaje, el mundo).
- **Prefab:** un "molde" de personaje u objeto que se reutiliza. Warrior, Goblin, etc. son prefabs.
- **Componente:** una pieza que se le agrega a un objeto para darle una función (por ejemplo, la pieza de audio de un personaje).
- **Arrastrar:** mantené apretado el clic sobre un archivo en Project, llevalo hasta el casillero del Inspector y soltá.

### Tres reglas que evitan perder trabajo
1. **Nunca cambies nada mientras el juego está en Play** (botón ▶ arriba al centro). Todo lo que cambies en Play se pierde al salir. Primero apretá ▶ de nuevo para parar, y recién ahí editá.
2. **Guardá seguido.**
   - Cambios en una **escena** o en un **prefab**: **Ctrl + S**.
   - Cambios en archivos como `AudioConfig`: **File > Save Project**.
3. **Si tus compañeros trabajan en el mismo proyecto,** no edites la misma escena o el mismo prefab que otra persona está tocando al mismo tiempo. Avisen por el grupo antes de subir cambios.

---

## 0. Preparación (se hace una sola vez)

En cada punto: primero **revisá** si ya está hecho. Solo hacelo si falta.

### 0.1 Revisá que exista la configuración de audio
1. En la ventana **Project**, buscá la carpeta `Assets` y dentro de ella `Resources`.
2. Entrá a `Resources` y buscá un archivo llamado **AudioConfig**.
3. Si lo ves, perfecto: hacé clic en él y seguí al punto 5.
4. Si NO lo ves: en el menú de arriba **Mishra > Audio > Create Audio Config**. Se crea solo y queda seleccionado.
5. Con `AudioConfig` seleccionado, mirá el Inspector. Tienen que aparecer estos títulos en negrita y en mayúsculas: *VOLUMENES INICIALES*, *SONIDOS DE INTERFAZ*, *SONIDOS DE INVENTARIO Y MISIONES*, *MUSICA POR ESCENA* y *EFECTOS 3D*.
6. Guardá con **File > Save Project**.

### 0.2 Revisá que cada escena tenga UN solo "oído" (Audio Listener)
El **Audio Listener** es el "oído" del juego: lo que se oye depende de dónde esté. Tiene que haber **uno solo por escena y estar en la cámara principal (Main Camera)**. Si falta o hay dos, los sonidos del mundo (pasos, golpes, ambiente) se oyen mal o no se oyen.

Las escenas del juego son: `MainMenuScene`, `NetworkMenu`, `CharacterSelect` y `Scene1`. Repetí estos pasos **en cada una**:

1. En **Project** andá a `Assets/Scenes` y hacé **doble clic** en la escena para abrirla. Si Unity pregunta si querés guardar la escena anterior, elegí **Save** si hiciste cambios, o **Don't Save** si no.
2. En la ventana **Hierarchy**, arriba hay un buscador (una lupa). Escribí exactamente: `t:AudioListener` y apretá Enter. La lista se reduce a los objetos que tienen un oído.
3. Mirá el resultado:
   - **Aparece solo `Main Camera`:** correcto. Andá al paso 4.
   - **Aparece otro objeto además** (por ejemplo `InventoryPreviewCamera`): seleccionalo, mirá en el Inspector el título **Audio Listener**, hacé clic en los **tres puntitos (⋮)** de ese título y elegí **Remove Component**.
   - **No aparece nada:** seleccioná `Main Camera` > abajo del Inspector botón **Add Component** > escribí `Audio Listener` > clic en el resultado.
4. Seleccioná `Main Camera` y en el Inspector mirá el título **Audio Listener**: la casilla de la izquierda del nombre tiene que estar **tildada**. Si no lo está, tildala.
5. Guardá la escena con **Ctrl + S**.
6. Borrá el texto del buscador de la Hierarchy.

Cómo darte cuenta del problema jugando: en la **Console** aparece un mensaje `[Audio] Habia mas de un AudioListener...`. Si lo ves, repetí estos pasos en esa escena.

### 0.3 Creá las carpetas de audio
1. En **Project**, revisá si ya existe `Assets/Audio`. Si no existe: clic derecho sobre `Assets` > **Create > Folder** > escribí `Audio` > Enter.
2. Dentro de `Audio` creá, si faltan: `Music`, `SFX` y `Ambience`.
3. Dentro de `SFX` creá, si faltan: `Player`, `Enemies`, `UI` y `World`.

### 0.4 Cómo preparar cada archivo de audio
1. Poné nombres claros, sin espacios ni tildes: `sfx_sword_hit_01`, `mus_menu_theme`, `ui_click`.
2. Formatos: **WAV** para sonidos cortos (golpes, clics). **OGG** o **MP3** para música y ambientes largos.
3. Arrastrá los archivos desde tu computadora a la carpeta que corresponda en la ventana **Project**.
4. Hacé clic en un audio y mirá el Inspector. Ajustá:
   - **Música y ambientes largos:** en *Load Type* elegí **Streaming**.
   - **Sonidos cortos:** en *Load Type* elegí **Decompress On Load**.
   - **Sonidos del mundo** (pasos, golpes, rugidos): tildá **Force To Mono**. Así se oye bien de qué lado vienen.
5. Abajo del Inspector apretá el botón **Apply** para guardar el cambio. Si no lo apretás, no se guarda.

### 0.5 Registrá de dónde sacaste cada sonido
Si bajaste un sonido de internet, tenés que anotar el autor y la licencia.
1. Revisá si existe el archivo `Assets/Audio/AUDIO_CREDITS.md`.
2. Si no existe, creá un archivo de texto con ese nombre desde tu editor de texto o desde el explorador de archivos de tu computadora, dentro de la carpeta `Assets/Audio` del proyecto.
3. Por cada sonido anotá una línea con: nombre del archivo, autor, link de descarga y licencia.

---

## 1. Sonidos de menús, botones e inventario

Se configuran en el archivo `Assets/Resources/AudioConfig`. Si dejás un casillero vacío, simplemente no suena y no da error.

### 1.1 Cómo llenar un casillero (se repite para todos)
1. En **Project**, entrá a `Assets/Resources` y hacé clic en **AudioConfig**.
2. En el Inspector buscá el casillero (por ejemplo **Boton Click**) y abrilo con la flechita (▶) que está a su izquierda.
3. Adentro vas a ver **Sonidos**. Abrilo con su flechita.
4. Poné el número de elementos: buscá el campo **Size** (o el botón **+**) y dejalo en **1**. Aparece un hueco vacío que dice *None (Audio Clip)*.
5. Desde **Project**, **arrastrá** el audio al hueco.
6. Si querés que a veces suene uno y a veces otro, poné **Size** en 2, 3, etc. y arrastrá un audio a cada hueco. Se elige uno al azar cada vez.
7. Opcional:
   - **Volumen:** de 0 (mudo) a 1 (máximo).
   - **Tono Minimo / Tono Maximo:** variación para que no suene siempre idéntico.
   - **Espera Minima:** segundos mínimos entre dos repeticiones.
8. **Sonido 3D** tiene que estar **sin tildar** en todos los sonidos de menú e interfaz.
9. Guardá con **File > Save Project**.

### 1.2 Botones y paneles
| Casillero | Cuándo suena |
|---|---|
| Boton Encima | al pasar el mouse por un botón |
| Boton Click | al hacer clic en un botón |
| Abrir Panel | al abrir el inventario (tecla Tab) |
| Cerrar Panel | al cerrar el inventario |
| Error | cuando falla un crafteo (faltan materiales, oro o espacio) |
| Confirmar | todavía no lo usa ningún botón; queda listo para más adelante |

### 1.3 Revisá que un botón haga sonido
Un botón suena solo si tiene el componente **Themed Button**.
1. Seleccioná el botón (en la Hierarchy, o abriendo su prefab con doble clic).
2. En el Inspector, buscá un título que diga **Themed Button**.
3. Si NO está: el botón tiene que tener antes un componente **Button** (aparece en el Inspector). Después tocá **Add Component** (abajo del Inspector), escribí `Themed Button` y hacé clic en el resultado.
4. Guardá con **Ctrl + S**.

### 1.4 Sonidos de inventario y misiones
Más abajo en el mismo `AudioConfig`, bloque **SONIDOS DE INVENTARIO Y MISIONES**. Se llenan igual que en 1.1.

| Casillero | Cuándo suena |
|---|---|
| Equipar | al equipar un arma o armadura **con el inventario abierto** |
| Craftear | al craftear con éxito (si falla suena **Error**) |
| Recoger Item | al recoger un objeto del suelo |
| Ganar Oro | al recibir oro; al gastarlo no suena |
| Mision Completada | al completar una misión que se está mostrando en pantalla |

Estos sonidos los oye solo el jugador que hace la acción.

### 1.5 Probalo
1. Poné un audio en **Boton Click** y guardá (**File > Save Project**).
2. Abrí la escena `MainMenuScene` (doble clic en `Assets/Scenes`).
3. Apretá ▶ (Play) arriba al centro.
4. Hacé clic en un botón. Tiene que sonar.
5. Apretá ▶ otra vez para salir de Play.
6. Si no suena, andá a **"Si algo no suena"** al final.

---

## 2. Música de cada escena

1. En **Project** abrí `Assets/Resources` y hacé clic en **AudioConfig**.
2. En el Inspector buscá el título **MUSICA POR ESCENA** y abrí la lista **Musica Por Escena** que está debajo con la flechita.
3. En **Size** (o con el botón **+**) poné cuántas escenas van a tener música. Por ejemplo, 4.
4. Cada elemento tiene tres campos:
   - **Escena:** escribí el nombre de la escena **tal cual**, respetando mayúsculas.
   - **Musica:** arrastrá la canción desde Project.
   - **Volumen:** de 0 a 1.
5. Los nombres de escena del proyecto son:
   - `MainMenuScene`
   - `NetworkMenu`
   - `CharacterSelect`
   - `Scene1`
6. Si dudás de un nombre, revisalo en **File > Build Profiles**: arriba aparece la **Scene List** con las escenas del juego.
7. Reglas importantes:
   - Si una escena **no está en la lista**, sigue sonando la música de la escena anterior. Así el tema del menú puede seguir en las pantallas de red y de personaje.
   - Si una escena **está en la lista pero sin canción**, queda en silencio.
   - **Segundos De Transicion** (justo debajo de la lista de escenas) es lo que dura el cambio suave entre dos canciones.
8. **File > Save Project**.

---

## 3. Sonidos de un personaje (jugadores y enemigos)

Se usa el componente **Character Audio**, que va en el **objeto principal** del prefab del personaje.

### 3.1 Revisá si el personaje ya tiene el componente
1. En **Project**, andá a `Assets/Prefabs` y entrá a `Players` (jugadores) o `Enemies` (enemigos).
2. **Doble clic** en el prefab (por ejemplo `Warrior`). Se abre en modo edición: la Hierarchy ahora muestra solo ese personaje.
3. En la Hierarchy hacé clic en el **primer objeto de la lista** (el que tiene el nombre del personaje, arriba de todo).
4. En el Inspector, recorré los títulos y buscá **Character Audio**.
5. Si está: pasá al punto 3.2.
6. Si NO está: abajo del Inspector botón **Add Component** > escribí `Character Audio` > clic en el resultado (aparece como *Mishra Audio > Character Audio*).
7. Personajes que tienen que tenerlo: Warrior, Mage, Hunter, Goblin, Orc, Slime, Acolyte, DemiGod y cualquier enemigo nuevo.

**Atención con enemigos nuevos:** si un enemigo puede atacar, **tiene que** tener Character Audio. Si no, al atacar en el editor aparece un error rojo en la Console (`MissingComponentException`).

Un NPC pacífico (por ejemplo un personaje que solo habla) no lo necesita.

### 3.2 Revisá que el personaje tenga también el sincronizador de vida
Los sonidos de daño, curación, muerte y reaparición dependen de otro componente.
1. Con el mismo objeto principal seleccionado, buscá en el Inspector un componente llamado **Character Stats Sync Controller**.
2. Si no está, avisale a quien arma los personajes. No lo agregues sin consultar.

### 3.3 Llenar los casilleros del personaje
1. En el componente **Character Audio** arrastrá audios a los casilleros, igual que en 1.1 (**Size = 1**, arrastrar al hueco).
2. Guardá con **Ctrl + S**.
3. Para salir del prefab, tocá la flechita **◀** arriba a la izquierda de la Hierarchy.

| Casillero | Cuándo suena | Cómo se detecta |
|---|---|---|
| Ataque Ligero / Ataque Pesado / Ataque Especial | al atacar | Jugadores: por la animación. Enemigos: lo avisa el combate |
| Recibe Dano | al perder vida | automático |
| Curacion | al recuperar bastante vida de golpe (pociones) | automático |
| Muerte | cuando la vida llega a 0 | automático |
| Reaparece | al volver a la vida | automático |
| Paso | al caminar o correr | automático |
| Salto / Aterrizaje | al saltar y al caer | automático |

### 3.4 Revisá la casilla "Ataques Desde Animacion"
En el mismo componente hay una casilla llamada **Ataques Desde Animacion**.
- **Jugadores** (Warrior, Mage, Hunter): tiene que estar **tildada**.
- **Enemigos:** tiene que estar **sin tildar**.

**No renombres los estados del Animator de los jugadores:** `AttackLight`, `AttackHeavy` y `AttackSpecial`. El sistema usa esos nombres para saber cuándo suena cada ataque. Si se cambian, los ataques de los jugadores dejan de sonar.

### 3.5 Ajustes de cada casillero
- **Volumen**, **Tono Minimo / Maximo**, **Espera Minima** (evita que un sonido suene ametrallado).
- **Sonido 3D:** tildado = se oye más fuerte cuanto más cerca estás. Dejalo **tildado** para pasos, golpes y rugidos.
- **Curacion Minima:** la curación solo suena si recupera ese porcentaje de la vida máxima (evita que suene con la regeneración lenta).
- **Pasos y aterrizaje:** si no suenan, revisá **Capas De Suelo** y que incluya la capa del terreno. También podés ajustar **Distancia Entre Pasos** y **Velocidad Minima**.

Todo lo que hace un personaje lo oyen todos los jugadores, no solo el dueño.

Los primeros 0,75 segundos después de que aparece un personaje no suena nada (evita ruidos raros al cargar).

### 3.6 Otro sonido según la animación (opcional: un rugido, un hechizo)
Esto es opcional. Si no lo necesitás, dejalo vacío.
1. En **Character Audio**, abrí la lista **Por Estado De Animacion**, que está debajo del título **OTROS SONIDOS SEGUN LA ANIMACION**.
2. En **Size** (o **+**) poné 1 (o más).
3. En **Estado** escribí el nombre **exacto** del estado en el Animator. Para ver los nombres: en el Inspector, clic en los **tres puntitos (⋮)** del título **Character Audio** y elegí **Listar estados del Animator**. Los nombres aparecen en la **Console**.
4. Arrastrá el audio a **Sonido**.
5. **Retraso:** segundos que pasan desde que empieza la animación hasta que suena, para que coincida con el golpe.
6. Guardá con **Ctrl + S**.

---

## 4. Sonidos sueltos (cofre, puerta, trampa)

1. Seleccioná el objeto que va a sonar (por ejemplo, el cofre). Si no hay un objeto, creá uno vacío: clic derecho en la Hierarchy > **Create Empty**.
2. Abajo del Inspector: **Add Component** > escribí `Sound Player` > clic en el resultado (*Mishra Audio > Sound Player*).
3. En **Sonido** arrastrá el audio, igual que en 1.1. Dejá **Sonido 3D** tildado para sonidos del mundo.
4. Si querés que suene apenas aparezca o se active el objeto, tildá **Sonar Al Activarse**.
5. **Categoria:** dejá *Efectos*, salvo que sea un sonido de menú (entonces *Interfaz*).
6. Guardá con **Ctrl + S**.

Para que lo dispare un botón:
1. Seleccioná el botón y buscá el componente **Button** en el Inspector.
2. Abajo encontrá **On Click ()** y hacé clic en el botón **+**.
3. Arrastrá desde la Hierarchy el objeto que tiene el Sound Player al hueco que dice *None (Object)*.
4. En el desplegable de la derecha elegí **SoundPlayer > Play2D** (suena igual en todos lados).
5. Guardá con **Ctrl + S**.

---

## 5. Sonido ambiente (fogata, río, viento)

1. Creá un objeto vacío en el lugar donde va a sonar: clic derecho en la Hierarchy > **Create Empty**. Ponele un nombre claro, por ejemplo `Ambiente_Fogata`.
2. Con el objeto seleccionado, movelo con las flechas de colores de la ventana **Scene** hasta donde corresponda.
3. **Add Component** > escribí `Ambient Sound` > clic en el resultado.
4. Arrastrá el audio a **Sonido**. Conviene un audio que se pueda repetir en bucle sin que se note el corte.
5. Ajustá:
   - **Volumen:** de 0 a 1.
   - **Distancia Minima:** hasta esa distancia suena a volumen completo.
   - **Distancia Maxima:** más lejos que eso ya no se oye.
6. En la ventana **Scene** vas a ver dos esferas alrededor del objeto: la chica y la grande son esas dos distancias.
7. Se controla con el volumen de **Efectos** del jugador.
8. Guardá la escena con **Ctrl + S**.

---

## 6. Música que cambia en una zona (pueblo, cueva, jefe)

1. Creá un objeto vacío en el centro de la zona (clic derecho en la Hierarchy > **Create Empty**) y ponele un nombre claro, por ejemplo `Musica_Cueva1`.
2. **Add Component** > escribí `Music Area` > clic en el resultado.
3. Arrastrá la canción a **Musica**. Ajustá **Volumen**.
4. **Radio:** el tamaño de la zona en metros. Al seleccionar el objeto se ve una esfera rosada en la ventana **Scene**: cambiá el número hasta que cubra toda la zona.
5. **Margen De Salida:** metros extra que hay que alejarse para que se considere que salió. Sirve para que la música no cambie una y otra vez en el borde.
6. Al entrar a la zona, la música cambia con una transición suave. Al salir vuelve la de la escena.
7. Si dos zonas se superponen, suena la de la zona a la que entraste último.
8. Guardá la escena con **Ctrl + S**.

---

## 7. Volúmenes del jugador (menú de Opciones)

Hay cuatro volúmenes: **General, Música, Efectos e Interfaz**. Se guardan solos en la computadora de cada jugador.

1. Abrí la escena o el prefab donde está el panel de Opciones.
2. Revisá si ya hay sliders. Si faltan: clic derecho dentro del panel de Opciones en la Hierarchy > **UI > Slider**. Hacé uno por cada volumen.
3. Seleccioná el panel de Opciones (el objeto que contiene a los sliders).
4. **Add Component** > escribí `Volume Settings UI` > clic en el resultado.
5. En el Inspector aparecen cuatro huecos: **General, Musica, Efectos, Interfaz**. Arrastrá desde la Hierarchy cada slider a su hueco. Los que no uses los dejás vacíos.
6. No hace falta configurar valores mínimo ni máximo en los sliders: se ajustan solos entre 0 y 1.
7. Guardá con **Ctrl + S**.

Los volúmenes iniciales (cuando nadie tocó nada) se ponen en `AudioConfig`, en el bloque **VOLUMENES INICIALES**. Una vez que un jugador los cambia, manda lo que él guardó.

---

## Si algo no suena
Revisá en este orden:

1. **¿Hay un audio en el casillero y el volumen no está en 0?** Abrí `AudioConfig` o el prefab y mirá que **Sonidos** tenga un audio arrastrado.
2. **¿Existe `Assets/Resources/AudioConfig`?** Mirá el punto 0.1. Si falta, la Console muestra `[Audio] No hay Resources/AudioConfig.asset`.
3. **¿Hay un solo Audio Listener y está en la Main Camera?** Mirá el punto 0.2. Es la causa más común de que no se oigan pasos, ataques ni ambiente.
4. **¿Los volúmenes están en 0?** Si alguien los bajó jugando, quedan guardados. Para resetearlos: menú **Edit > Clear All PlayerPrefs**.
5. **Música de escena:** ¿el nombre de **Escena** coincide exacto con la escena? (`MainMenuScene`, no `MainMenu`).
6. **Personajes:**
   - ¿El prefab tiene **Character Audio** y lo guardaste con Ctrl + S?
   - ¿Tiene **Character Stats Sync Controller**? Sin él no suenan daño, curación, muerte ni reaparición.
   - Jugadores: **Ataques Desde Animacion** tildado. Enemigos: sin tildar.
7. **Sonidos 3D que se oyen poco o nada:** más lejos que **Distancia Maxima** (por defecto 45 metros, en `AudioConfig`, bloque **EFECTOS 3D**) no se oyen.
8. **Pasos y salto:** ¿**Capas De Suelo** incluye la capa del terreno?
9. **Botones:** ¿el botón tiene **Themed Button**? Mirá el punto 1.3.
10. **Console:** abrí la pestaña **Console** y buscá mensajes que empiecen con `[Audio]`. Si no entendés uno, copialo y pasalo al grupo.