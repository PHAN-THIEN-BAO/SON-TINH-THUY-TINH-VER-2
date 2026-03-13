# 🎮 Son-Tinh-Thuy-Tinh - Multi-Player Multi-Scene System

## 📖 Giới thiệu

Hệ thống quản lý nhiều player trong nhiều scene khác nhau cho Unity. Cho phép 2 hoặc nhiều player tồn tại ở các scene khác nhau cùng lúc, với khả năng di chuyển qua lại giữa các scene thông qua portal.

### ✨ Tính năng chính

- ✅ **Multi-Player Support**: Hỗ trợ 2+ players cùng lúc
- ✅ **Multi-Scene Management**: Mỗi player có thể ở scene khác nhau
- ✅ **Scene Transitions**: Portal system để chuyển scene
- ✅ **Position Persistence**: Lưu vị trí player tại mỗi scene
- ✅ **Split-Screen**: Hỗ trợ split-screen cho local multiplayer
- ✅ **Additive Scene Loading**: Load/unload scene động để tối ưu memory
- ✅ **DontDestroyOnLoad**: Player và GameManager persistent suốt game

---

## 🚀 QUICKSTART

**Muốn bắt đầu ngay?** → Đọc [QUICKSTART.md](./QUICKSTART.md)

Setup hoàn tất trong 5 phút!

---

## 📚 TÀI LIỆU

### 📘 Hướng dẫn đầy đủ
- **[MULTI_PLAYER_MULTI_SCENE_GUIDE.md](./MULTI_PLAYER_MULTI_SCENE_GUIDE.md)** - Tài liệu chính, đầy đủ nhất
  - Kiến trúc tổng quan
  - Sơ đồ luồng hoạt động
  - Chi tiết các component
  - Hướng dẫn setup từng bước
  - So sánh các phương án
  - FAQ

### 🏗️ Kiến trúc & Sơ đồ
- **[ARCHITECTURE_DIAGRAM.md](./ARCHITECTURE_DIAGRAM.md)** - Sơ đồ kiến trúc chi tiết
  - Class diagrams
  - Data flow
  - Scene lifecycle
  - Memory layout
  - Transition flowcharts

### 🔧 Troubleshooting
- **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** - Xử lý lỗi và best practices
  - Common errors & solutions
  - Debug techniques
  - Performance optimization
  - Testing checklist

### ⚡ Quick Reference
- **[QUICKSTART.md](./QUICKSTART.md)** - Setup nhanh 5 phút

---

## 🗂️ SCRIPTS

Tất cả scripts nằm trong `Assets/Sprirt/Daft/`:

### Core Scripts
- **`GameManager.cs`** - Singleton quản lý toàn bộ hệ thống
- **`PlayerData.cs`** - Lưu trữ dữ liệu và trạng thái player
- **`ScenePortal.cs`** - Cổng dịch chuyển giữa các scene
- **`SceneSpawnPoint.cs`** - Đánh dấu vị trí spawn

### Helper Scripts
- **`PlayerController.cs`** - Controller mẫu cho player movement
- **`MultiSceneSetupHelper.cs`** - Unity Editor tools (Menu: Tools → Multi-Scene Setup)

---

## 🎯 KIẾN TRÚC TỔNG QUAN

```
Persistent Scene (luôn load)
    ├── GameManager (DontDestroyOnLoad)
    ├── Player 1 (DontDestroyOnLoad)
    └── Player 2 (DontDestroyOnLoad)

Scene A - "Son tinh" (load/unload động)
    ├── SpawnPoint
    ├── Portal → Scene B
    └── Environment

Scene B - "Thuy Tinh" (load/unload động)
    ├── SpawnPoint
    ├── Portal → Scene A
    └── Environment
```

**Chi tiết:** Xem [ARCHITECTURE_DIAGRAM.md](./ARCHITECTURE_DIAGRAM.md)

---

## 🛠️ SETUP NHANH

### 1. Build Settings
```
File → Build Settings → Add Scenes:
  [0] Persistent.unity
  [1] Son tinh.unity
  [2] Thuy Tinh.unity
```

### 2. Persistent Scene
```
Tools → Multi-Scene Setup:
  1. Create GameManager
  2. Create Player (x2)
```

### 3. Scene A & B
```
Mỗi scene cần:
  • SpawnPoint (Tools → Create Spawn Point)
  • Portal (Tools → Create Portal)
  • Ground/Terrain
```

### 4. Test
```
1. Open Persistent.unity
2. Press Play
3. Move players to portals
4. Press E to transition
```

**Chi tiết:** Xem [QUICKSTART.md](./QUICKSTART.md)

---

## 🎮 CONTROLS (Mặc định)

### Player 1
- **Move**: WASD hoặc Arrow Keys
- **Jump**: Space
- **Portal**: E (khi ở gần portal)

### Player 2
- **Move**: IJKL
- **Jump**: RightShift
- **Portal**: E

*Có thể customize trong PlayerController Inspector*

---

## 🔍 VALIDATION

Kiểm tra setup của bạn:

**Tools → Multi-Scene Setup → 5. Validate Setup**

Sẽ hiển thị báo cáo đầy đủ về:
- GameManager status
- Player count & configuration
- Spawn points
- Portals
- Build Settings

---

## ⚠️ YÊU CẦU

- Unity 2022.3+ (hoặc 2021.3+)
- .NET Standard 2.1
- Input System (Legacy hoặc New Input System)

---

## 🐛 TROUBLESHOOTING

### Lỗi thường gặp:

**Player bị mất khi chuyển scene?**
→ Kiểm tra DontDestroyOnLoad trong GameManager.RegisterPlayer

**Portal không hoạt động?**
→ Kiểm tra Collider Is Trigger = true và Target Scene Name

**Player spawn sai vị trí?**
→ Kiểm tra SceneSpawnPoint.sceneName khớp với tên scene

**Chi tiết:** Xem [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)

---

## 📊 SO SÁNH PHƯƠNG ÁN

| Tiêu chí | Additive + DDOL | Single Scene | LoadScene Single |
|----------|-----------------|--------------|------------------|
| Multi-player multi-scene | ✅ | ⚠️ | ❌ |
| Hiệu năng | ✅ | ⚠️ | ✅ |
| Quản lý state | ✅ | ✅ | ⚠️ |
| Độ phức tạp | ⚠️ | ✅ | ✅ |
| Memory usage | ✅ | ❌ | ✅ |

**Chi tiết:** Xem [MULTI_PLAYER_MULTI_SCENE_GUIDE.md](./MULTI_PLAYER_MULTI_SCENE_GUIDE.md#so-sánh-các-phương-án)

---

## 💡 BEST PRACTICES

1. **Naming**: Scene names phải khớp chính xác (case-sensitive!)
2. **Organization**: Persistent scene chỉ chứa DontDestroyOnLoad objects
3. **Cameras**: Mỗi scene phụ không nên có Main Camera
4. **Lighting**: Dùng baked lighting, disable shadows cho lights thừa
5. **Debug**: Sử dụng validation tool thường xuyên

---

## 🎓 EXAMPLES

### Thêm player thứ 3:
```csharp
// 1. Duplicate Player 2 trong Persistent scene
// 2. Rename: "Player 3"
// 3. Configure PlayerData: playerName = "Player 3"
// 4. Configure input axes (Horizontal3, Vertical3)
// Done! System tự động hỗ trợ
```

### Thêm scene thứ 3:
```csharp
// 1. Tạo scene mới: "Scene C"
// 2. Add vào Build Settings
// 3. Tạo SpawnPoint với sceneName = "Scene C"
// 4. Tạo Portal từ Scene A/B → Scene C
// Done!
```

---

## 🤝 CONTRIBUTING

Có ý tưởng cải thiện? Contributions welcome!

---

## 📝 LICENSE

[Thêm license của bạn ở đây]

---

## 📞 SUPPORT

Gặp vấn đề?
1. Đọc [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)
2. Check Console errors
3. Run validation tool
4. Review [MULTI_PLAYER_MULTI_SCENE_GUIDE.md](./MULTI_PLAYER_MULTI_SCENE_GUIDE.md)

---

## 📈 ROADMAP

- [ ] Save/Load system
- [ ] Online multiplayer support (Netcode)
- [ ] More portal types (one-way, timed, conditional)
- [ ] Scene streaming optimization
- [ ] Mobile support

---

**Happy coding! 🚀**