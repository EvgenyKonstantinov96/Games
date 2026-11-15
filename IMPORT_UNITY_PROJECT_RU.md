# Как загрузить существующую Unity-игру в репозиторий Games

**Важно:** в репозитории пока нет полного Unity-проекта Zombie Survival Run v0.8. Файлы README и .gitignore — только подготовка.

## Через GitHub Desktop (предпочтительно)

1. Установите GitHub Desktop: https://desktop.github.com/ и войдите в свой GitHub-аккаунт.
2. Выберите **File → Clone repository** и репозиторий `EvgenyKonstantinov96/Games`.
3. Откройте папку, в которую клонировали `Games`, в Проводнике Windows.
4. Откройте папку уже установленного на ПК рабочего Unity-проекта Zombie Survival Run (можно предыдущую версию, которую удалось скачать).
5. Скопируйте в `Games` **только** папки `Assets`, `Packages`, `ProjectSettings` и при наличии нужную документацию.
6. Вернитесь в GitHub Desktop, в поле Summary укажите `Import existing Zombie Survival Run Unity project`, нажмите **Commit to main**, затем **Push origin**.
7. Проверьте, что на https://github.com/EvgenyKonstantinov96/Games появились папки `Assets`, `Packages`, `ProjectSettings`.

Не копируйте папки `Library`, `Temp`, `Obj`, `Logs`, `Build` и `UserSettings`. Они создаются или обслуживаются локально.

**Следующий шаг после загрузки:** сообщите в диалоге ChatGPT, что исходники опубликованы, и уточните, какая именно версия была скопирована (например, v0.7.0). Тогда изменения можно будет вносить в файлы репозитория с помощью подключённого GitHub.

## Почему через GitHub Desktop?

Архив, созданный в ChatGPT, может не скачиваться из интерфейса браузера. Подключение GitHub может менять исходники в удалённом репозитории, но не обязательно способно напрямую переносить ZIP из временного хранилища ChatGPT. Загрузка уже существующей локальной копии через GitHub Desktop обходит эту проблему.
