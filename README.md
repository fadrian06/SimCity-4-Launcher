# SimCity 4 Launcher

![Banner de SimCity 4 Launcher](Images/Gemini_Generated_Image_b604web604web604.jpg)

Aplicación de escritorio WinForms para seleccionar e iniciar perfiles de SimCity 4. El launcher conserva los accesos directos existentes y ofrece una interfaz propia con lista de perfiles, fondos personalizados y acciones para crear, editar o eliminar perfiles.

El código fuente se mantiene separado del programa compilado en `Source\SimCity 4 Launcher.cs`. El ejecutable permanece en la carpeta principal para que el acceso directo y las rutas de recursos no cambien.

## Inicio

Abre `SimCity 4 Launcher.lnk` en el Escritorio. El acceso directo inicia `C:\SimCity 4 Launcher\SimCity 4 Launcher.exe` y usa el icono `C:\Games\SimCity 4\SimCity4.ico`. La ventana principal usa una barra de título personalizada, con botones cuadrados de 38 × 38 px e iconos centrados, y se maximiza al iniciarse.

## Perfiles e imágenes

- Cada perfil corresponde a un acceso directo local `Profiles\SC4 (nombre).lnk`, donde se conservan los argumentos, el destino, el directorio de trabajo y el icono. Esta carpeta no se sube a GitHub ni se incluye en las releases. Si no hay perfiles, pulsa `Nuevo perfil` y selecciona `SimCity 4.exe` para crear el primero.
- El último perfil seleccionado se guarda en `%LOCALAPPDATA%\SimCity 4 Launcher\last-profile.txt`.
- Los fondos elegidos desde el editor se copian a `Backgrounds`, una carpeta local excluida de GitHub y de las releases. Se admiten PNG, JPEG, BMP, GIF, TIFF y WebP; las rutas se guardan en `%LOCALAPPDATA%\SimCity 4 Launcher\profile-backgrounds.txt` y las referencias antiguas se actualizan automáticamente al cargar.
- Las imágenes WebP se decodifican con `Imazen.WebP` y el runtime nativo agrupados en `Libraries`.

## Recursos visuales

- `Fonts\Orbitron-variable.ttf` se usa para los títulos, con Trebuchet MS como fuente de respaldo y para los textos de lectura; `Fonts\OFL.txt` contiene su licencia.
- Los iconos de Bootstrap Icons están en `Icons\Bootstrap Icon *.png`; su licencia está en `Icons\Bootstrap Icons LICENSE.txt`.
- `C:\Games\SimCity 4\SimCity4.ico` se incrusta directamente en `SimCity 4 Launcher.exe` durante la compilación; el acceso directo del Escritorio conserva ese mismo icono.
- Las bibliotecas WebP y sus avisos están en `Libraries`.
- El logo del sidebar es `Images\vltqk99kevp81.png`.
- El banner del proyecto que se muestra al inicio de este README es `Images\Gemini_Generated_Image_b604web604web604.jpg`.

## Compilar y verificar

Desde PowerShell, ejecuta el script de compilación:

```powershell
& '.\Build-Launcher.ps1'
```

El script cierra las instancias del launcher, compila desde `Source\SimCity 4 Launcher.cs` hacia esta carpeta usando la biblioteca `Libraries\Imazen.WebP.dll` e incrusta `C:\Games\SimCity 4\SimCity4.ico` en el ejecutable. Luego actualiza `SimCity 4 Launcher.lnk` en el Escritorio y vuelve a abrir el launcher. Comprueba los perfiles, la interfaz y la decodificación de todos los fondos guardados con:

```powershell
& 'C:\SimCity 4 Launcher\SimCity 4 Launcher.exe' '--verify'
```

El código de salida `0` indica que la verificación pasó. Esta comprobación no inicia el juego ni guarda la preferencia del último perfil.

## Releases beta automáticas

`.github/workflows/beta-release.yml` compila en Windows y publica una GitHub prerelease secuencial `v0.1.0-beta.N` por cada push a una rama. El ZIP contiene el ejecutable, `Fonts`, `Icons`, `Images`, `Libraries` y este README; excluye los perfiles y fondos locales. Extrae el ZIP en una carpeta escribible, inicia el ejecutable y crea un perfil seleccionando `SimCity 4.exe`. El build remoto no incrusta el icono instalado localmente en `C:\Games\SimCity 4\SimCity4.ico`; el código fuente del repositorio y las instrucciones de compilación siguen disponibles.

`Profiles` y `Backgrounds` son datos locales y no se publican. El workflow solo recibe permiso `contents: write` para crear releases; no publica paquetes ni solicita permisos `packages`. GitHub no ofrece un ajuste o endpoint compatible para desactivar GitHub Packages por repositorio, así que esa función no se puede apagar desde esta automatización.

## Mantenimiento

Al finalizar cada tarea, actualiza este `README.md` y `AGENTS.md` para reflejar el estado, los cambios y la verificación. Mantén esta instrucción vigente en ambos documentos.

## Estado de esta actualización

- Los 11 iconos Bootstrap locales son PNG transparentes y se comprueba su carga con `--verify`; el acceso directo apunta al ejecutable y conserva el icono existente `C:\Games\SimCity 4\SimCity4.ico`. El nombre se muestra directamente sobre el fondo del perfil, sin placa opaca.
- Se eliminaron los fundidos y transiciones de la interfaz para evitar parpadeos; al cambiar el hover solo se invalida la fila anterior y la actual (si están visibles), no toda la lista.
- El nombre del perfil se dibuja directamente sobre la imagen de fondo, sin etiqueta ni placa detrás. `Jugar` comparte su posición vertical y los 64 px de alto, queda alineado a la derecha y mide como máximo 260 px, menos que el área del nombre.
- El menú de acciones del perfil usa un renderer oscuro también detrás de los iconos, con imágenes de 16 × 16 px centradas dentro de su margen; el indicador de selección cubre la altura completa de la fila. El icono de tres puntos del dropdown aparece solo al pasar el cursor por un perfil y el menú requiere clic izquierdo sobre ese indicador visible; el clic derecho y el resto de la fila no lo abren. La lista no se solapa con la barra de desplazamiento, por lo que el indicador permanece accesible al pasar el cursor.
- Se mejoró la jerarquía tipográfica: cuerpo y lista en Trebuchet MS de 12 pt; selección en 12.5 pt negrita; títulos Orbitron de 11 pt y nombre del perfil de 32 pt; botones de 12–15 pt en negrita.
- La compilación y `--verify` finalizaron correctamente; la verificación comprueba la selección inicial, transparencia, altura y carga de iconos. El launcher quedó abierto al terminar esta tarea.
- En esta tarea se documentó en `README.md` y `AGENTS.md` que ambos archivos deben actualizarse al finalizar cada tarea; el launcher se abrió sin recompilar.
- En esta tarea, los recursos se agruparon en `Icons`, `Libraries`, `Fonts`, `Profiles`, `Backgrounds` e `Images`; `--verify` terminó con código 0, incluido el fondo WebP migrado, y el launcher quedó abierto.
- En esta tarea, el código fuente se separó en `Source\SimCity 4 Launcher.cs`; se actualizó el script para compilar desde allí y conservar el ejecutable en la carpeta principal. La compilación y `--verify` terminaron con código 0, el acceso directo conserva su destino y el launcher quedó abierto.
- En esta tarea, `Iniciar` se hizo más estrecho, el nombre pasó a dibujarse directamente sin fondo propio y el cambio entre imágenes evita borrar primero el fondo actual. No hay animaciones ni transiciones; la compilación y `--verify` terminaron con código 0, el acceso directo conserva su destino e icono y el launcher quedó visible y maximizado.
- En esta tarea, se configuró la compilación para incrustar `C:\Games\SimCity 4\SimCity4.ico` directamente en `SimCity 4 Launcher.exe`; la extracción del icono y `--verify` se comprobaron, y el launcher quedó abierto.
- En esta tarea, se corrigió el parpadeo al pasar el cursor sobre los perfiles limitando el repintado a las filas afectadas; la compilación y `--verify` pasaron, y el launcher quedó abierto.
- En esta tarea, se ajustaron minimizar, maximizar y cerrar a 38 × 38 px, centrando sus iconos (o el texto alternativo) tanto horizontal como verticalmente. `--verify` comprueba esa geometría y alineación; la compilación y la verificación terminaron con código 0.
- En esta tarea, se corrigieron los colores y el margen del dropdown de acciones, y se extendió el indicador del perfil activo a toda la fila. La prueba previa falló con código 9 y, después del arreglo, compilación y `--verify` pasaron con código 0; el acceso directo conserva su destino y el launcher quedó abierto.
- En esta tarea, el botón principal pasó a decir `Jugar` y se centraron los iconos del dropdown de acciones; compilación correcta, `--verify` devolvió código 0 y el launcher quedó abierto.
- En esta tarea, se dejó visible el indicador de dropdown en cada perfil y se limitó la apertura del menú al clic izquierdo sobre ese indicador; `--verify` comprobó que no se abra al pulsar el resto de la fila o con clic derecho y terminó con código 0. La compilación terminó correctamente y el launcher quedó abierto con una ventana visible.
- En esta tarea, `Nuevo perfil` recibió estilo de acción principal con icono y estados de hover/pulsación; el indicador del dropdown ahora aparece solo al pasar el cursor y el clic izquierdo sobre él es el único que abre el menú. La compilación terminó correctamente, `--verify` devolvió código 0 y el launcher quedó abierto.
- En esta tarea, se separó la lista de la barra de desplazamiento para que el indicador de acciones se pueda mostrar y usar; se reajustó el espacio y la alineación de texto/iconos en los botones del editor. El color primario ahora usa el tono de fondo del logo (`#9D5880`) con texto claro; compilación correcta, `--verify` devolvió código 0 y el launcher quedó abierto.
- En esta tarea, el cambio de selección dejó de repintar la lista completa al abrir el dropdown y ahora solo invalida las filas afectadas. Los botones del editor centran icono y texto y mantienen huecos de 8 px; `--verify` comprobó la regresión y el layout, terminó con código 0 y el launcher quedó abierto.
- Se inicializó un repositorio Git en `C:\SimCity 4 Launcher`; `.gitignore` excluye el ejecutable generado. `Profiles` y `Backgrounds` son carpetas locales ignoradas y no se versionan.
- El repositorio público está en [GitHub](https://github.com/fadrian06/SimCity-4-Launcher). Los perfiles y fondos locales no se incluyen en el árbol principal, el historial depurado ni las nuevas releases.
- En esta tarea, se añadió un workflow de release beta por push a cada rama, con un ZIP compilado y permiso de Actions limitado a `contents: write`. Se actualizaron la descripción y los topics del repositorio. GitHub no proporciona un control compatible para desactivar Packages por repositorio.
- Se copió el banner `Gemini_Generated_Image_b604web604web604.jpg` a `Images` y se incrustó al principio de este README; el ZIP de releases también lo incluye porque empaqueta esa carpeta. La descripción del repositorio en GitHub se cambió al español y se conservaron sus siete topics.
- `Profiles` y `Backgrounds` se eliminaron de todos los commits de `main` y de los ZIP de release; las carpetas locales se preservaron. Se retiraron las releases y tags beta1 y beta2 que apuntaban al historial anterior.
- El launcher ahora inicia sin perfiles y permite crear el primero al seleccionar `SimCity 4.exe`; `--verify` valida el estado vacío.
- La beta pública [`v0.1.0-beta.3`](https://github.com/fadrian06/SimCity-4-Launcher/releases/tag/v0.1.0-beta.3) pasó la verificación: el ZIP contiene el ejecutable y el banner, sin rutas de perfiles o fondos.