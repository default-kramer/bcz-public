#lang racket/gui

(module+ main
  (save-images))

(require pict)
(require (prefix-in htdp: 2htdp/image)); star make-color))

; public static readonly Godot.Color Bronze = Godot.Color.Color8(205, 127, 50);
; public static readonly Godot.Color Silver = Godot.Color.Color8(192, 192, 192);
; public static readonly Godot.Color Gold = Godot.Color.Color8(255, 215, 0);
(define bronze (htdp:make-color 205 127 50))
(define silver (htdp:make-color 192 192 192))
(define gold (htdp:make-color 255 215 0))

; public static readonly Godot.Color Green = Godot.Color.Color8(60, 175, 36);
(define green (make-color 60 175 36))

(define (medal color)
  ; Warning - the Godot UI will not currently scale this image.
  ; (The HBoxContainer is ignoring the TextureRect's `scale` property?)
  ; So the size here is important:
  (define size 16)
  (htdp:star size 'solid color))

(define (checkmark)
  (dc (lambda (dc dx dy)
        (define old-brush (send dc get-brush))
        (define old-pen (send dc get-pen))
        (send dc set-brush (new brush% [style 'transparent]))
        (send dc set-pen (new pen% [color green] [width 3]))
        (define path (new dc-path%))
        (send path move-to 2 13)
        (send path line-to 5 18)
        (send path line-to 21 7)
        (send dc draw-path path dx dy)
        (send dc set-brush old-brush)
        (send dc set-pen old-pen))
      ; match size that `star` generates
      26 25))

(define (save-images)
  (define results '())
  (define directory (current-directory))
  (define (save-bitmap pict filename)
    (let* ([bmp (pict->bitmap pict 'aligned)]
           [filename (format "~a~a" directory filename)]
           [quality 100])
      (set! results (cons pict (cons filename results)))
      (send bmp save-file filename 'bmp quality)))
  (save-bitmap (checkmark) "checkmark.bmp")
  (save-bitmap (medal bronze) "bronze.bmp")
  (save-bitmap (medal silver) "silver.bmp")
  (save-bitmap (medal gold) "gold.bmp")
  ; return value
  (reverse results))
