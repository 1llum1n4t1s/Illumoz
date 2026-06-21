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
#include <stdlib.h>
#include <string.h>

/* C# (NativeAOT) エクスポート。 */
extern int  mozc_ibus_init(void);                         /* controller 初期化(IPC 結線) */
extern int  mozc_ibus_process_key(uint32_t keyval, uint32_t state);
extern int  mozc_ibus_get_focused_index(void);
extern int  mozc_ibus_get_preedit(char *buf, int cap);
extern int  mozc_ibus_get_commit(char *buf, int cap);
extern int  mozc_ibus_get_candidates(char *buf, int cap); /* 改行区切り候補列 */

static gboolean
mozc_process_key_event(IBusEngine *engine, guint keyval, guint keycode, guint state)
{
    /* キーリリースは無視(press のみ処理)。 */
    if (state & IBUS_RELEASE_MASK) {
        return FALSE;
    }

    int consumed = mozc_ibus_process_key((uint32_t)keyval, (uint32_t)state);

    /* commit 文字列があれば確定。固定バッファに収まらない長文(辞書の長語句や
       長い TEXT_INPUT)は、必要長で動的確保して取りこぼさない。 */
    char commit_buf[1024];
    int n = mozc_ibus_get_commit(commit_buf, sizeof(commit_buf));
    if (n > 0) {
        char *commit = commit_buf;
        char *heap = NULL;
        if (n > (int)sizeof(commit_buf) - 1) {
            /* cap 不足。WriteUtf8 は必要バイト数 n を返すので n+1 で確保し再取得する。 */
            heap = (char *)malloc((size_t)n + 1);
            if (heap != NULL && mozc_ibus_get_commit(heap, n + 1) == n) {
                commit = heap;
            } else {
                commit = NULL; /* 確保失敗時は確定を諦める(部分挿入はしない)。 */
            }
        }
        if (commit != NULL) {
            commit[n] = '\0';
            IBusText *t = ibus_text_new_from_string(commit);
            if (t != NULL) {
                ibus_engine_commit_text(engine, t);
            }
        }
        free(heap);
    }

    /* preedit を更新表示。長い貼り付け/合成テキストが固定バッファ(1023B)を超える場合は、
       commit/candidate と同様に必要長で動的確保して再取得する。超過時に更新を飛ばすと
       IBus が前回の marked text を出したままになり現在の preedit に同期しない。 */
    char preedit_buf[1024];
    int p = mozc_ibus_get_preedit(preedit_buf, sizeof(preedit_buf));
    if (p >= 0) {
        char *preedit = preedit_buf;
        char *preedit_heap = NULL;
        if (p > (int)sizeof(preedit_buf) - 1) {
            /* cap 不足。WriteUtf8 は必要バイト数 p を返すので p+1 で確保し再取得する。 */
            preedit_heap = (char *)malloc((size_t)p + 1);
            if (preedit_heap != NULL && mozc_ibus_get_preedit(preedit_heap, p + 1) == p) {
                preedit = preedit_heap;
            } else {
                preedit = NULL; /* 確保失敗時は残存表示を明示クリアして不整合を防ぐ。 */
            }
        }
        if (preedit != NULL) {
            preedit[p] = '\0';
            IBusText *pt = ibus_text_new_from_string(preedit);
            /* cursor は「文字数」で渡す。バイト数(p)を渡すと日本語等の多バイト文字で
               カーソル位置がずれるため g_utf8_strlen で文字数に換算する。 */
            guint cursor = (guint)g_utf8_strlen(preedit, p);
            ibus_engine_update_preedit_text(engine, pt, cursor, p > 0);
        } else {
            ibus_engine_hide_preedit_text(engine);
        }
        free(preedit_heap);
    }

    /* 候補列(改行区切り)を lookup table として表示。長いユーザー辞書語や多数の候補で
       固定バッファを超える場合は、commit と同様に必要長で動的確保して取りこぼさない
       (超過時に丸ごと候補窓を消さない)。 */
    char cands_buf[4096];
    int c = mozc_ibus_get_candidates(cands_buf, sizeof(cands_buf));
    char *cands = cands_buf;
    char *cands_heap = NULL;
    if (c > (int)sizeof(cands_buf) - 1) {
        /* cap 不足。WriteUtf8 は必要バイト数 c を返すので c+1 で確保し再取得する。 */
        cands_heap = (char *)malloc((size_t)c + 1);
        if (cands_heap != NULL && mozc_ibus_get_candidates(cands_heap, c + 1) == c) {
            cands = cands_heap;
        } else {
            cands = NULL; /* 確保失敗時は候補表示を諦める(部分表示はしない)。 */
        }
    }
    if (cands != NULL && c > 0) {
        cands[c] = '\0';
        IBusLookupTable *table = ibus_lookup_table_new(9, 0, TRUE, TRUE);
        char *save = NULL;
        for (char *tok = strtok_r(cands, "\n", &save); tok; tok = strtok_r(NULL, "\n", &save)) {
            ibus_lookup_table_append_candidate(table, ibus_text_new_from_string(tok));
        }
        /* 注目候補(サーバの focused_index)を lookup table のカーソルに反映する。
           -1(未注目)のときは既定の先頭のままにする。 */
        int focused = mozc_ibus_get_focused_index();
        if (focused >= 0) {
            ibus_lookup_table_set_cursor_pos(table, (guint)focused);
        }
        ibus_engine_update_lookup_table(engine, table, TRUE);
    } else {
        ibus_engine_hide_lookup_table(engine);
    }
    free(cands_heap);

    return consumed ? TRUE : FALSE;
}

/* engine class init で process-key-event を差し替える。 */
void mozc_ibus_install(IBusEngineClass *klass)
{
    klass->process_key_event = mozc_process_key_event;
}

/* --- IBusEngine サブタイプ(GObject)定義 --- */
typedef struct _IBusMozcEngine      { IBusEngine parent; }      IBusMozcEngine;
typedef struct _IBusMozcEngineClass { IBusEngineClass parent; } IBusMozcEngineClass;

G_DEFINE_TYPE(IBusMozcEngine, ibus_mozc_engine, IBUS_TYPE_ENGINE)

static void ibus_mozc_engine_class_init(IBusMozcEngineClass *klass)
{
    mozc_ibus_install(IBUS_ENGINE_CLASS(klass));
}

static void ibus_mozc_engine_init(IBusMozcEngine *engine) { (void)engine; }

/* ibus-engine-mozc 実行ファイルのエントリポイント。
 * ibus に接続し、mozc エンジンの factory を登録してメインループを回す。
 * --ibus 付きで ibus デーモンから起動される(mozc.xml component の exec)。 */
int main(int argc, char **argv)
{
    (void)argc;
    (void)argv;

    ibus_init();

    /* C# 側 controller(IPC トランスポート)を初期化。失敗時は起動を中止。 */
    if (!mozc_ibus_init()) {
        g_warning("mozc_ibus_init failed; aborting");
        return 1;
    }

    IBusBus *bus = ibus_bus_new();
    g_object_ref_sink(bus);
    if (!ibus_bus_is_connected(bus)) {
        g_warning("cannot connect to ibus daemon");
        return 1;
    }

    IBusFactory *factory = ibus_factory_new(ibus_bus_get_connection(bus));
    g_object_ref_sink(factory);
    /* エンジン名は component XML(dist/ibus/mozc.xml)の <engine><name> と一致させる。 */
    ibus_factory_add_engine(factory, "mozc-jp", ibus_mozc_engine_get_type());

    /* コンポーネント名も XML の <name> と一致させる。一致しないと ibus-daemon が
     * 広告コンポーネントとプロセスを結び付けられず、起動できてもアクティブ化に失敗する。 */
    /* ibus_bus_request_name は guint32 を返し、失敗時 0。失敗ならメインループに入らない。 */
    if (!ibus_bus_request_name(bus, "com.google.IBus.Mozc", 0)) {
        g_warning("cannot request ibus service name");
        return 1;
    }

    ibus_main();
    return 0;
}
