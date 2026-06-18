# ibus 極薄 native stub のビルド(実機 Linux)。
# C# 側を `dotnet publish Mozc.Os.Linux -r linux-x64 -p:NativeLib=Shared -p:PublishAot=true`
# で libMozc.Os.Linux.so にしてから、この stub をリンクして ibus-engine-mozc を作る。
cmake_minimum_required(VERSION 3.16)
project(ibus_mozc C)
find_package(PkgConfig REQUIRED)
pkg_check_modules(IBUS REQUIRED ibus-1.0)
add_executable(ibus-engine-mozc ibus_mozc.c)
target_include_directories(ibus-engine-mozc PRIVATE ${IBUS_INCLUDE_DIRS})
target_link_libraries(ibus-engine-mozc PRIVATE ${IBUS_LIBRARIES}
    ${CMAKE_CURRENT_SOURCE_DIR}/../bin/Release/net10.0/linux-x64/publish/Mozc.Os.Linux.so)
# 配置: /usr/lib/ibus-mozc/ + /usr/share/ibus/component/mozc.xml(dist/ibus/mozc.xml)
