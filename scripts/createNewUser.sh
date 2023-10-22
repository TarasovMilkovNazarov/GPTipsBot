!/bin/bash

# Запрос имени пользователя
read -p "Введите имя пользователя: " username

# Запрос публичного SSH-ключа
read -p "Введите публичный SSH-ключ: " ssh_key

# Проверка наличия обоих аргументов
if [ -z "$username" ] || [ -z "$ssh_key" ]; then
  echo "Имя пользователя и публичный SSH-ключ оба обязательны."
  exit 1
fi

# Создание пользователя
sudo useradd -m -d /home/$username -s /bin/bash $username

# Создание директории .ssh в домашней директории пользователя
sudo su - $username
mkdir .ssh

# Добавление публичного ключа в файл authorized_keys
touch .ssh/authorized_keys
echo $ssh_key > /home/$username/.ssh/authorized_keys

# Установка правильных разрешений
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

echo "Пользователь $username создан и настроен для входа по SSH с использованием предоставленного ключа. Перезапусти ВМ"