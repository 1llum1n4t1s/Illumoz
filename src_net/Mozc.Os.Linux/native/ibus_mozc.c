/* ibus 極薄 native stub。GObject signal(process-key-event)を受け、C# 側
 * (NativeAOT 共有ライブラリ Mozc.Os.Linux)のエクスポート関数へ転送するだけ。
 * 変換ロジック・IPC は全て C#(IbusBridge → ImeClient → mozc_server)。
 * 実機 Linux で glib/ibus ヘッダを使ってコンパイルする(この sandbox では未コンパイル)。
 *
 * ビルド例:
 *   gcc -shared -fPIC ibus_mozc.c -o ibus-engine-mozc \
 *       $(pkg-config --cflags --libs ibus-1.0) -L. -lMozc.Os.Linux
 */
#include <ibus.h>
#include <stdint.h>

/* C# (NativeAOT) エクスポート。 */
extern int  mozc_ibus_process_key(uint32_t keyval, uint32_t state);
extern int  mozc_ibus_get_preedit(char *buf, int cap);
extern int  mozc_ibus_get_commit(char *buf, int cap);

static gboolean
mozc_process_key_event(IBusEngine *engine, guint keyval, guint keycode, guint state)
{
    /* キーリリースは無視(press のみ処理)。 */
    if (state & IBUS_RELEASE_MASK) {
        return FALSE;
    }

    int consumed = mozc_ibus_process_key((uint32_t)keyval, (uint32_t)state);

    /* commit 文字列があれば確定。 */
    char commit[1024];
    int n = mozc_ibus_get_commit(commit, sizeof(commit));
    if (n > 0 && n < (int)sizeof(commit)) {
        commit[n] = '\0';
        IBusText *t = ibus_text_new_from_string(commit);
        ibus_engine_commit_text(engine, t);
    }

    /* preedit を更新表示。 */
    char preedit[1024];
    int p = mozc_ibus_get_preedit(preedit, sizeof(preedit));
    if (p >= 0 && p < (int)sizeof(preedit)) {
        preedit[p] = '\0';
        IBusText *pt = ibus_text_new_from_string(preedit);
        ibus_engine_update_preedit_text(engine, pt, p, p > 0);
    }

    return consumed ? TRUE : FALSE;
}

/* engine class init で process-key-event を差し替える。 */
void mozc_ibus_install(IBusEngineClass *klass)
{
    klass->process_key_event = mozc_process_key_event;
}
