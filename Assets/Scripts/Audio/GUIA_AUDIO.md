# Guía de audio de Mishra (para quien pone los sonidos)

No hace falta programar nada. Todo se hace arrastrando archivos de audio a casilleros en el Inspector.

## 0. Una sola vez
1. Menú superior: **Mishra > Audio > Create Audio Config**. Se crea `Assets/Resources/AudioConfig`.
2. Guardá los audios en carpetas ordenadas, por ejemplo:
   `Assets/Audio/Music`, `Assets/Audio/SFX/Player`, `SFX/Enemies`, `SFX/UI`, `Ambience`.
3. Nombres claros: `sfx_sword_hit_01`, `mus_menu_theme`, `ui_click`.
4. Efectos cortos: WAV. Música y ambiente largos: OGG o MP3 (en el Inspector del archivo, "Load Type: Streaming").
5. Anotá de dónde bajaste cada sonido (autor, link, licencia) en `AUDIO_CREDITS.md`.

## 1. Sonidos de menús y botones
Abrí `Assets/Resources/AudioConfig` y arrastrá audios a:
- **Boton Encima**: al pasar el mouse por un botón.
- **Boton Click**: al hacer clic.
- **Abrir Panel / Cerrar Panel / Error / Confirmar**: quedan listos para usar.

Todos los botones que tienen **Themed Button** suenan solos. No hay que tocar cada botón.

## 2. Música por escena
En el mismo `AudioConfig`, en **Musica Por Escena**, agregá un elemento por escena:
- **Escena**: el nombre exacto (`MainMenu`, `Scene1`...).
- **Musica**: la canción. Si la dejás vacía, esa escena queda en silencio.

Si una escena no está en la lista, sigue sonando la música anterior. Así el tema del menú continúa en las pantallas de red y de clase.

## 3. Sonidos de un personaje (Warrior, Mage, Hunter, Goblin, Orc, Slime...)
1. En la ventana Project, doble clic al prefab del personaje (por ejemplo `Warrior`).
2. En el Inspector: **Add Component > Mishra Audio > Character Audio**.
3. Arrastrá los audios a los casilleros. Si ponés varios audios en un casillero, se elige uno al azar cada vez.
4. Salí del prefab con la flecha de volver y guardá (Ctrl + S).

| Casillero | Cuándo suena | Cómo se detecta |
|---|---|---|
| Ataque Ligero / Pesado / Especial | al atacar | Jugadores: por la animación. Enemigos: lo avisa el combate |
| Recibe Dano | al perder vida | automático |
| Curacion | al recuperar bastante vida (pociones) | automático |
| Muerte | cuando la vida llega a 0 | automático |
| Reaparece | al volver a la vida | automático |
| Paso | al caminar o correr | automático |
| Salto / Aterrizaje | al saltar y al caer | automático |

**Jugadores:** dejá marcado **Ataques Desde Animacion**.
**Enemigos:** desmarcalo. Sus ataques los avisa el sistema de combate.

Cada casillero tiene: **Volumen**, **Tono** (variación para que no suene siempre igual), **Espera Minima** (evita que suene ametrallado) y **Sonido 3D** (más fuerte cuanto más cerca estás; para sonidos del mundo dejarlo marcado).

Todo se oye en todos los jugadores, no solo en el que hace la acción.

### Otro sonido de un enemigo (rugido, hechizo...)
En **Otros Sonidos Segun La Animacion** agregá un elemento: escribí el **Estado** (nombre del estado en el Animator) y arrastrá el audio. **Retraso** sirve para que suene justo en el golpe. Para ver los nombres de estados: menú de los tres puntos del componente > **Listar estados del Animator**.

## 4. Sonidos sueltos (cofre, puerta, trampa)
1. Objeto vacío (o el mismo objeto) > **Add Component > Mishra Audio > Sound Player**.
2. Arrastrá el audio. Marcá **Sonar Al Activarse** si debe sonar solo al aparecer.
3. Para dispararlo desde un botón: en su **On Click** elegí el objeto y la función `SoundPlayer > Play2D`.

## 5. Sonido ambiente (fogata, río, viento)
Objeto vacío en el mapa > **Add Component > Mishra Audio > Ambient Sound**. Arrastrá el audio y ajustá las distancias (al seleccionar el objeto se ven dos esferas: volumen completo y límite de audición).

## 6. Música que cambia en una zona (pueblo, cueva, jefe)
Objeto vacío en el centro de la zona > **Add Component > Mishra Audio > Music Area**. Arrastrá la canción y ajustá el **Radio** (se ve como una esfera). Al entrar cambia la música con una transición suave; al salir vuelve la de la escena.

## 7. Volúmenes del jugador (para quien arme el menú de Opciones)
En el panel de Opciones agregá **Mishra Audio > Volume Settings UI** y arrastrá los Sliders (General, Música, Efectos, Interfaz). Los valores se guardan solos.

## Si algo no suena
- ¿Existe `Assets/Resources/AudioConfig`? (Mishra > Audio > Create Audio Config)
- ¿El casillero tiene un audio arrastrado y el volumen no está en 0?
- ¿El prefab tiene **Character Audio**? ¿Guardaste el prefab?
- Los efectos 3D no se oyen si estás muy lejos del personaje (Distancia Maxima en `AudioConfig`).
- Mirá la pestaña **Console**: los avisos de audio empiezan con `[Audio]`.
