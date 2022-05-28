#lang racket/gui

(module+ main
  (save-images))

(require pict)

(define size 360)
(define size/2 180)
(define thickness 36)
(define thickness/2 18)
(define body-color (make-color 255 255 255))
(define border-color (make-color 220 220 220))

(define (single-catalyst)
  (cc-superimpose
   (blank size)
   (disk (- size thickness) #:color body-color
         #:border-color border-color #:border-width thickness)))

(define (joined-catalyst)
  (define path
    (let ([path (new dc-path%)])
      ; arc from right -> top -> left
      (let ([xy thickness/2]
            [wh (- size thickness)])
        (send path arc xy xy wh wh 0 pi))
      ; lines to left corner then right corner
      (let ([y (- size thickness/2)])
        (send path line-to thickness/2 y)
        (send path line-to y y))
      ; closing adds a line from right corner to start of arc
      (send path close)
      path))
  (dc (lambda (dc dx dy)
        (define old-brush (send dc get-brush))
        (define old-pen (send dc get-pen))
        (send dc set-brush (new brush% [color body-color]))
        (send dc set-pen (new pen%
                              [cap 'butt] ; doesn't work with draw
                              [width thickness]
                              [color border-color]))
        (send dc draw-path path dx dy)
        ; Looks like [cap 'butt] doesn't work with draw-path...?
        ; Patch over the rounded corners with some small rectangles
        (let ([y (+ dy size (- thickness/2))]
              [x0 dx]
              [x1 (+ dx size)]
              [wh thickness])
          (send dc draw-line x0 y x1 y))
        (send dc set-brush old-brush)
        (send dc set-pen old-pen))
      size size))

; In certain situations, using `#:color "transparent"` does not leave
; a transparent hole. It leaves a black hole instead.
; So you can paint it red instead and use this function.
(define (red->transparent pict)
  (define pixels (pict->argb-pixels pict 'unsmoothed))
  (for ([i (in-range 0 (bytes-length pixels) 4)])
    (let ([a (bytes-ref pixels i)]
          [r (bytes-ref pixels (+ 1 i))]
          [g (bytes-ref pixels (+ 2 i))]
          [b (bytes-ref pixels (+ 3 i))])
      (when (and (= 255 r) (= 0 g) (= 0 b))
        (bytes-set! pixels i 0)
        (bytes-set! pixels (+ 1 i) 0)
        (bytes-set! pixels (+ 2 i) 0)
        (bytes-set! pixels (+ 3 i) 0))))
  (argb-pixels->pict pixels (inexact->exact (pict-width pict))))

(define (enemy)
  (cc-superimpose
   (blank size)
   (let* ([size (- size thickness thickness thickness/2)]
          [thickness (+ thickness thickness)])
     (filled-rounded-rectangle
      size size #:color "black"
      #:draw-border? #t #:border-color border-color #:border-width thickness))))

(define (backdrop pict color)
  (cc-superimpose
   (filled-rectangle size size #:color color #:draw-border? #f)
   pict))

(backdrop (single-catalyst) "black")
(let ([j (backdrop (joined-catalyst) "black")])
  (ht-append (rotate j (/ pi 2))
             (rotate j (- (/ pi 2)))))

(blank 10)
(backdrop (enemy) "blue")

(define (save-images)
  (define results '())
  (define directory (current-directory))
  (define bgcolor (make-color 0 0 0 0))
  (define (save-bitmap pict filename)
    (let* ([pict (backdrop pict bgcolor)]
           [bmp (pict->bitmap pict 'aligned)]
           [filename (format "~a~a" directory filename)]
           [quality 100])
      (set! results (cons pict (cons filename results)))
      (send bmp save-file filename 'bmp quality)))
  ; joined
  (for ([rotation '(d r u l)])
    (let* ([pict (joined-catalyst)]
           [pict (rotate pict (case rotation
                                [(d) 0]
                                [(r) (/ pi 2)]
                                [(u) pi]
                                [(l) (- (/ pi 2))]))])
      (save-bitmap pict (format "joined-~a.bmp" rotation))))
  ; single
  (save-bitmap (single-catalyst) "single.bmp")
  ; enemy
  (save-bitmap (enemy) "enemy.bmp")
  ; return value
  (reverse results))
