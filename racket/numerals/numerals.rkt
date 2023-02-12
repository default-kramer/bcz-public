#lang racket/gui

(module+ main
  (save-images))

(require pict)

(define thickness 40)
(define thickness/2 20)
(define height 240)
(define height/2 120)
(define height/4 60)
(define margin-y 32)
(define spacing 20)
(define the-pen (send the-pen-list find-or-create-pen
                      "black" thickness 'solid 'round))

(define (make-path points)
  (let ([path (new dc-path%)])
    (send path move-to
          (car (first points))
          (cdr (first points)))
    (for ([point (cdr points)])
      (send path line-to (car point) (cdr point)))
    path))

(define (one)
  (dc (lambda (dc dx dy)
        (define old-pen (send dc get-pen))
        (send dc set-pen the-pen)
        (define path (make-path (list (cons thickness/2 margin-y)
                                      (cons thickness/2 (- height margin-y)))))
        (send dc draw-path path dx dy)
        (send dc set-pen old-pen))
      thickness height))

(define (four)
  (let ([w height/2]
        [mid height/4])
    (dc (lambda (dc dx dy)
          (define old-pen (send dc get-pen))
          (send dc set-pen the-pen)
          (define path (make-path (list (cons mid margin-y)
                                        (cons thickness/2 height/2)
                                        (cons mid (- height margin-y))
                                        (cons (- w thickness/2) height/2))))
          (send path close)
          (send dc draw-path path dx dy)
          (send dc set-pen old-pen))
        w height)))

(define (background pict)
  (let* ([w (+ (pict-width pict) spacing spacing spacing)]
         [bar (filled-rectangle w margin-y #:color "lightgray" #:draw-border? #f)])
    (vc-append bar
               (blank (- height margin-y margin-y))
               bar)))

(define (digits i)
  (define (get-picts i)
    (cond
      [(i . >= . 4)
       (cons (four) (get-picts (- i 4)))]
      [(i . >= . 1)
       (cons (one) (get-picts (- i 1)))]
      [else (list)]))
  (apply hc-append (cons spacing (get-picts i))))

(define (numeral i)
  (let* ([dig (digits i)]
         [bg (background dig)])
    (cc-superimpose bg dig)))

;(numeral 1)
;(numeral 2)
(numeral 3)
;(numeral 4)
;(numeral 5)
;(numeral 11)
;(numeral 12)
(numeral 13)


(define (save-images)
  (define results '())
  (define directory (current-directory))
  (define bgcolor (make-color 0 0 0 0))
  (define (save-bitmap pict filename)
    (let* ([bmp (pict->bitmap pict 'aligned)]
           [filename (format "~a~a" directory filename)]
           [quality 100])
      (set! results (cons pict (cons filename results)))
      (send bmp save-file filename 'bmp quality)))
  (for ([i (in-range 1 17)])
    (save-bitmap (digits i) (format "~a.bmp" i)))
  (format "Saved images to ~a" directory))
