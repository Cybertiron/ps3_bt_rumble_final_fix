# Alternatyva: „oficialus apėjimas" per ViGEmBus (be Test Mode)

> Šis dokumentas aprašo **kitą architektūrą**, kuri **nereikalauja Test Signing režimo**. Jis nėra
> šio repo driverio pataisos dalis — tai atskiras kelias tiems, kas nenori savo test-signed driverio.

## Problema

Mūsų pataisytas DsHidMini yra **test-signed** (savu sertifikatu), todėl reikia įjungti Windows
**Test Signing** režimą. Norint to išvengti, reikėtų arba oficialaus Microsoft parašo
([upstream PR](https://github.com/nefarius/DsHidMini/pull/460)), arba **visai kito požiūrio** —
nenaudoti savo driverio.

## Idėja

Nereikia rašyti (ir pasirašinėti) savo draiverio. Panaudok jau **oficialiai pasirašytą**
bendruomenės draiverį **ViGEmBus** (Virtual Gamepad Emulation Bus) kaip „tiltą", o visą logiką
laikyk **vartotojo lygmens programoje** (userland), kuri su draiveriu kalbasi per oficialią
**ViGEmClient** biblioteką.

Kadangi ViGEmBus pasirašytas Microsoft — **Test Mode nereikia.**

## Kaip tai veikia principu

1. Vartotojas **vieną kartą** įsidiegia oficialų, jau pasirašytą **ViGEmBus** draiverį (`.msi`).
2. Tavo programa nuskaito **raw** duomenis iš PS3 pultelio (per WinUSB arba HID).
3. Programa naudoja **ViGEmClient** biblioteką, kad PS3 mygtukus/ašis paverstų **Xbox 360** arba
   **DualShock 4** komandomis.
4. Biblioteka perduoda jas ViGEmBus draiveriui — ir Windows „mato" tikrą Xbox/DS4 pultelį.

```
[ PS3 pultelis ]                     [ tavo userland .exe ]                 [ ViGEmBus (signed) ]
   USB / BT  ── raw HID reportai ──►  input parse  ──►  ViGEmClient  ──────►  virtualus X360/DS4  ──► žaidimai
      ▲                                       │
      └──── rumble output report ◄─── feedback callback (rumble iš žaidimo)
```

## Ką reikia įgyvendinti (userland programa)

1. **Įvesties skaitymas (input).**
   - **Per USB:** paprasčiausia — atidaryti DS3 kaip HID/WinUSB įrenginį ir skaityti input reportus.
   - **Per Bluetooth:** DS3 BT nestandartinis, tad reikia arba jau suporinto BthPS3 sluoksnio
     (kuris atveria HID sąsają), arba pačiam tvarkyti L2CAP/HID. Tai **sunkiausia dalis** — būtent
     dėl to ir egzistuoja DsHidMini kaip draiveris.
2. **Virtualus pultelis (output).** Su ViGEmClient sukurk `Xbox360` (arba `DualShock4`) įrenginį ir
   kiekvieną kadrą siųsk mygtukų/ašių būseną.
3. **Rumble atgal (feedback).** Užsiregistruok ViGEmClient **feedback callback** — kai žaidimas
   siunčia vibraciją į virtualų pultą, gauni `LargeMotor`/`SmallMotor` reikšmes ir **pats parašai**
   jas į fizinį DS3 output reportą.

## ⚠️ Svarbu: rumble per Bluetooth galioja tas pats mūsų radinys

Jei šiame kelyje rašysi rumble į DS3 **per Bluetooth**, turi siųsti output reportą per **interrupt
kanalą** su `0xA2` (DATA|Output) prefiksu — **ne** per control kanalą (`0x52` Set_Report). Tai
lygiai ta pati priežastis, kurią radome tvarkydami DsHidMini (žr. pagrindinį README ir
[PR #460](https://github.com/nefarius/DsHidMini/pull/460)). Per control kanalą DS3 atnaujins LED,
bet **variklio neaktyvins.** Taigi mūsų reverse-engineering išvada galioja **abiem** keliams.

## Privalumai ir trūkumai

**Privalumai**
- ✅ **Nereikia Test Mode** — ViGEmBus oficialiai pasirašytas.
- ✅ Nereikia savo draiverio parašo (nei EV cert, nei Partner Center).
- ✅ Lengva plėsti (mapping, profiliai) — viskas userland kode.

**Trūkumai**
- ❌ Reikia nuolat veikiančios **background programos** (uždarei — pultelis „dingsta").
- ❌ **BT** įvesties/išvesties skaitymas iš userland yra sudėtingas (DS3 BT nestandartinis).
- ❌ Iš esmės atkartoja tai, ką DsHidMini jau daro XInput režimu — tik už driverio ribų.
- ❌ Papildoma latencija/CPU lyginant su kernel/UMDF keliu (praktiškai nedidelė).

## Kodo eskizas (C#, `Nefarius.ViGEm.Client`)

```csharp
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

var client = new ViGEmClient();                 // reikalauja idiegto ViGEmBus
var x360 = client.CreateXbox360Controller();

// rumble is zaidimo -> i fizini DS3
x360.FeedbackReceived += (s, e) =>
{
    byte large = e.LargeMotor;   // stiprus variklis
    byte small = e.SmallMotor;   // silpnas variklis
    // TODO: parasyk DS3 output reporta:
    //   USB  -> interrupt OUT endpoint
    //   BT   -> HID interrupt kanalas, prefiksas 0xA2 (NE 0x52!)
    Ds3WriteRumble(large, small);
};

x360.Connect();

while (true)
{
    var ds3 = Ds3ReadInput();                   // tavo HID/WinUSB skaitymas
    x360.SetButtonState(Xbox360Button.A, ds3.Cross);
    x360.SetAxisValue(Xbox360Axis.LeftThumbX, ds3.LX);
    // ... likusieji mygtukai/ašys ...
    x360.SubmitReport();
}
```

## Įrankiai ir nuorodos

- **ViGEmBus** (draiveris, signed): https://github.com/nefarius/ViGEmBus
- **ViGEmClient** (C/C++ API): https://github.com/nefarius/ViGEmClient
- **.NET wrapper** `Nefarius.ViGEm.Client` (NuGet)
- Realus pavyzdys, kaip toks userland tiltas atrodo praktikoje: **DS4Windows**, **x360ce**

## Kada rinktis ką

| Nori... | Rinkis |
|---|---|
| Sisteminio, „visada veikia" sprendimo be background app | **DsHidMini** (mūsų pataisa arba, po merge, oficialus) |
| Išvengti Test Mode dabar, sutinki laikyti veikiančią programą | **ViGEmBus + userland app** (šis dokumentas) |
| Ilgalaikio švaraus sprendimo visiems | Palaikyk [PR #460](https://github.com/nefarius/DsHidMini/pull/460) → oficialus signed build |
